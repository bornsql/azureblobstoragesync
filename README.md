# Azure Blob Storage Sync
Sync SQL Server backup files on Azure Blob Storage without worrying about infinite lease.

Run this tool on any machine that can access your SQL Server backup files using UNC, or the SQL Server instance itself.

Requires:
- Azure Blob Storage subscription
- .NET Framework 4.0
- Internet access!

The Sync and Restore components have a dependency on the *Microsoft.WindowsAzure.Storage* NuGet package. However, this package has its own associated packages, which are downloaded but never used:

- Microsoft.Data.Edm
- Microsoft.Data.OData
- Microsoft.Data.Services.Client
- Microsoft.WindowsAzure.ConfigurationManager
- Newtonsoft.Json
- System.Spatial

---

###Connection Configuration File

The `connections.config` file is required to provide credentials to the Azure Blob Storage account. The format of this file is as follows:

    <?xml version="1.0" encoding="utf-8"?>
    <connectionStrings>
    	<clear /> // Clears any other connectionStrings in the app.config file
    	<add name="AzureConnectionString" connectionString="DefaultEndpointsProtocol=https;AccountName=<storageAccount>;AccountKey=<redacted>" />
    </connectionStrings>

This connection string can be copied and pasted from the [Azure Portal](https://portal.azure.com), from the **Primary Connection String** for your storage account.
