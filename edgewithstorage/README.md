# Azure IoT Edge - Sample module writing to Azure Blob Storage on IoT Edge

Walkthrough of the configuration options and a custom C# demo module that will create a random file on disk and upload it to the local Storage module in Azure IoT Edge.
Azure Blob storage module is configured to copy the uploaded files into a container on the cloud.

## Running the demo

### Pre-requisites
- Azure IoT Hub
- Azure IoT Edge installed and configured with version 1.2 on Ubuntu (tested on 20.04)
- Azure Storage
- Azure Container registry
- Visual Studio Code and Docker CE
- Azure IoT Tools extension for Visual Studio Code, see https://docs.microsoft.com/en-us/azure/iot-edge/how-to-vs-code-develop-module?view=iotedge-2020-11

For any Blob Module setting refer to the official documentation: https://docs.microsoft.com/en-us/azure/iot-edge/how-to-store-data-blob?view=iotedge-2020-11

### Run this demo

1. Clone this repo.
1. Create a .env file with the following variables:
    ```
    CONTAINER_REGISTRY_USERNAME_registry1=<>
    CONTAINER_REGISTRY_PASSWORD_registry1="<>"
    CONTAINER_REGISTRY_ADDRESS_registry1="<>.azurecr.io"
    DEMO_LOCAL_STORAGECONNSTR="DefaultEndpointsProtocol=http;BlobEndpoint=http://storeonedge:11002/mylocalaccount;AccountName=mylocalaccount;AccountKey=<key_from_Env-needs to be base64 encoded>"
    AZ_STORAGE_MODULE_KEY="=<key_from_Env>"
    AZ_CLOUD_STORAGE_CONNSTR="<YOUR_CLOUD_CONNECTIONSTRING - see docs>"
    ```
1. Update Docker Binds for the interactiondemo and Storage modules in the deployment templates `deployment.template.json` and `deployment.debug.template.json`.
    - `/edgefordemo/:/appdata/` > the /edgefordemo/ would refer to a local folder on the edge device, with the right permissions as described in the official docs.
    - For storage module, the example below assumes a local folder `/edge/containerdata`:
    ```
    "Binds": [
        "/edge/containerdata:/blobroot"
     ]
    ```
1. Build and push the containers, and deploy to your IoT Edge device.
1. To test file creation and copying between local Azure Blobl module and the cloud, you can use a Direct Method of the `interactiondemo` module: `GenerateFile` can be called through the Azure Portal to run a file creation and upload to local storage.
1. To debug in Visual Studio Code, opening this folder instead of the parent one will allow you to use the debug config.




