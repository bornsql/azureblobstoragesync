# What Is It?

Azure Blob Storage Backup is a feature built right into SQL Server, but it is only available in SQL Server 2012 Service Pack 1 CU 2 and higher.

Furthermore, there is no ability in any version of SQL Server prior to 2016, to back up to more than one location simultaneously. You have to choose between local backups or Azure Blob Storage. It is not possible to pick both. This is especially problematic if you perform Differential Backups.

**AzureBlobStorageSync** is a free tool that allows you to continue backing up your database locally, for any version of SQL Server, and synchronise your files to Azure Blob Storage on a schedule of your choosing.

It can work alongside your existing backup process, and leverages [Ola Hallengren's](https://ola.hallengren.com/) Maintenance Solution.

There is a companion restore tool, **AzureBlobStorageRestore**, also free, which is able to download the latest database (including Full, Differential and Transaction Log Backups), only knowing the name of the database, and build a restore script. This is especially useful if you suffer a catastrophic failure and have no other knowledge of the backups (e.g. the content of the msdb database).

### How Do I Do It?

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

###Connection Configuration File

The `connections.config` file is required to provide credentials to the Azure Blob Storage account. The format of this file is as follows:

    <?xml version="1.0" encoding="utf-8"?>
    <connectionStrings>
    	<clear /> // Clears any other connectionStrings in the app.config file
    	<add name="AzureConnectionString" connectionString="DefaultEndpointsProtocol=https;AccountName=<storageAccount>;AccountKey=<redacted>" />
    </connectionStrings>

This connection string can be copied and pasted from the [Azure Portal](https://portal.azure.com), from the **Primary Connection String** for your storage account.
