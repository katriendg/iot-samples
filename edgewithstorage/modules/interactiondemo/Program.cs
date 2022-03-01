namespace interactiondemo
{
    using System;
    using System.IO;
    using System.Runtime.InteropServices;
    using System.Runtime.Loader;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Devices.Client;
    using Microsoft.Azure.Devices.Client.Transport.Mqtt;

    using Azure.Storage.Blobs;


    class Program
    {
        static int counter;

        static void Main(string[] args)
        {
            Init().Wait();

            // Wait until the app unloads or is cancelled
            var cts = new CancellationTokenSource();
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
            WhenCancelled(cts.Token).Wait();
        }

        /// <summary>
        /// Handles cleanup operations when app is cancelled or unloads
        /// </summary>
        public static Task WhenCancelled(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }

        /// <summary>
        /// Initializes the ModuleClient and sets up the callback to receive
        /// messages containing temperature information
        /// </summary>
        static async Task Init()
        {
            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSetting };

            // Open a connection to the Edge runtime
            ModuleClient ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
            await ioTHubModuleClient.OpenAsync();
            Console.WriteLine("IoT Hub module client initialized.");

            // Register callback to be called when a message is received by the module
            await ioTHubModuleClient.SetInputMessageHandlerAsync("input1", PipeMessage, ioTHubModuleClient);

            // Register callback for Direct Method (for testing)
            await ioTHubModuleClient.SetMethodHandlerAsync("SampleDM", SampleDM, ioTHubModuleClient);
            await ioTHubModuleClient.SetMethodHandlerAsync("GenerateFile", GenerateFile, ioTHubModuleClient);

        }

        /// <summary>
        /// This method is called whenever the module is sent a message from the EdgeHub. 
        /// It just pipe the messages without any change.
        /// It prints all the incoming messages.
        /// </summary>
        static async Task<MessageResponse> PipeMessage(Message message, object userContext)
        {
            int counterValue = Interlocked.Increment(ref counter);

            var moduleClient = userContext as ModuleClient;
            if (moduleClient == null)
            {
                throw new InvalidOperationException("UserContext doesn't contain " + "expected values");
            }

            byte[] messageBytes = message.GetBytes();
            string messageString = Encoding.UTF8.GetString(messageBytes);
            Console.WriteLine($"Received message: {counterValue}, Body: [{messageString}]");

            if (!string.IsNullOrEmpty(messageString))
            {
                using (var pipeMessage = new Message(messageBytes))
                {
                    foreach (var prop in message.Properties)
                    {
                        pipeMessage.Properties.Add(prop.Key, prop.Value);
                    }
                    await moduleClient.SendEventAsync("output1", pipeMessage);

                    Console.WriteLine("Received message sent");
                }
            }
            return MessageResponse.Completed;
        }


        //test for writing output of a mapped file from disk
        private static async Task<MethodResponse> SampleDM(MethodRequest methodRequest, object userContext)
        {

            var moduleClient = userContext as ModuleClient;
            if (moduleClient == null)
            {
                throw new InvalidOperationException("UserContext doesn't contain " + "expected values");
            }

            string myFilename = "/appdata/myfile.txt";
            string fileContents = File.ReadAllText(myFilename);

            Console.WriteLine($"Execute DM, reading all text from {myFilename}");
            Console.WriteLine($"File contents: {fileContents}");

            Message opcMessage = new Message(Encoding.ASCII.GetBytes(fileContents));
            await moduleClient.SendEventAsync("output1", opcMessage);
            Console.WriteLine("SampleDM send message via output: output1");

            string result = $"{{\"result\":\"Successfully executed Direct method: {methodRequest.Name}\"}}";
            return new MethodResponse(Encoding.UTF8.GetBytes(result), 200);

        }


        //test for writing output of a mapped file from disk
        private static async Task<MethodResponse> GenerateFile(MethodRequest methodRequest, object userContext)
        {

            Console.WriteLine("Calling GenerateFile method");

            var moduleClient = userContext as ModuleClient;
            if (moduleClient == null)
            {
                throw new InvalidOperationException("UserContext doesn't contain " + "expected values");
            }

            BlobServiceClient blobServiceClient = null;
            BlobContainerClient containerClient = null;
            BlobClient blobClient = null;

            try
            {

                Random rnd = new Random();
                string localPath = "/appdata/";
                string myFilename = $"file{rnd.Next()}.txt";
                string fullPath = Path.Combine(localPath, myFilename);
                string fileContents = $"this is a sample and my filename and location is {fullPath}";
                File.WriteAllText(fullPath, fileContents);
                Console.WriteLine("Wrote local file");

                ///sample code for loading into blob module
                try
                {
                    Console.WriteLine("Creating blob client");
                    string connectionString = Environment.GetEnvironmentVariable("localstorageconnstring");
                    blobServiceClient = new BlobServiceClient(connectionString);
                }
                catch (System.Exception e)
                {
                    Console.WriteLine($"Error in creating blobserviceclient {e.Message}");
                    string msg = $"{{\"result\":\"Error executing Direct method: {methodRequest.Name}\"}}";
                    return new MethodResponse(Encoding.UTF8.GetBytes(msg), 500);
                }

                //Create a unique name for the container
                string containerName = "containerdata";

                try
                {
                    // Create the container and return a container client object
                    Console.WriteLine("Creating container");
                    containerClient = blobServiceClient.GetBlobContainerClient(containerName);
                    await containerClient.CreateIfNotExistsAsync();

                    // Get a reference to a blob
                    blobClient = containerClient.GetBlobClient(myFilename);

                    Console.WriteLine("Uploading to Blob storage as blob:\n\t {0}\n", blobClient.Uri);

                    // Upload data from the local file
                    Console.WriteLine("Uploading to endpoint");
                    await blobClient.UploadAsync(fullPath, true);

                    Console.WriteLine($"Execute DM, create a new random file {myFilename}");
                    Console.WriteLine($"File contents: {fileContents}");

                    Message opcMessage = new Message(Encoding.ASCII.GetBytes(fileContents));
                    await moduleClient.SendEventAsync("output1", opcMessage);
                    Console.WriteLine("SampleDM send message via output: output1");

                }
                catch (System.Exception e)
                {
                    Console.WriteLine($"Error in blob container create or blob upload {e.Message}");
                    string msg = $"{{\"result\":\"Error executing Direct method: {methodRequest.Name}\"}}";
                    return new MethodResponse(Encoding.UTF8.GetBytes(msg), 500);
                }

            }
            catch (System.Exception e)
            {
                Console.WriteLine($"Error in executing method:  {e.Message}");
                string msg = $"{{\"result\":\"Error executing Direct method: {methodRequest.Name}\"}}";
                 return new MethodResponse(Encoding.UTF8.GetBytes(msg), 500);

            }

            string result = $"{{\"result\":\"Successfully executed Direct method: {methodRequest.Name}\"}}";
            return new MethodResponse(Encoding.UTF8.GetBytes(result), 200);

        }

    }
}
