# What Is It?

Azure Blob Storage Backup is a feature built right into SQL Server, but it is only available in SQL Server 2012 Service Pack 1 CU 2 and higher.

Furthermore, there is no ability in any version of SQL Server prior to 2016, to back up to more than one location simultaneously. You have to choose between local backups or Azure Blob Storage. It is not possible to pick both. This is especially problematic if you perform Differential Backups.

**AzureBlobStorageSync** is a free tool that allows you to continue backing up your database locally, for any version of SQL Server, and synchronise your files to Azure Blob Storage on a schedule of your choosing.

**FileStorageSync** allows you to synchronise your files to a UNC path on a schedule of your choosing.

It can work alongside your existing backup process, and leverages [Ola Hallengren's](https://ola.hallengren.com/) Maintenance Solution.

There is a companion restore tool, **AzureBlobStorageRestore** (and **FileStorageRestore**), also free, which is able to download the latest database (including Full, Differential and Transaction Log Backups), only knowing the name of the database, and build a restore script. This is especially useful if you suffer a catastrophic failure and have no other knowledge of the backups (e.g. the content of the *msdb* database).

## Compatibility note

_**2021-06-17:** This version of the tool will be replaced by a .NET Core 3.1 LTS version in the near future, with updated Azure Storage libraries. The new version is currently in testing._

## How Do I Do It?

Run this tool on any machine that can access your SQL Server backup files using UNC, or the SQL Server instance itself.

The AzureBlobStorageSync and Restore requires:

- Azure Blob Storage subscription
- .NET Framework 4.7.2
- Internet access!

The FileStorageSync and Restore requires:

- .NET Framework 4.7.2

The Sync and Restore components have a dependency on the *Microsoft.WindowsAzure.Storage* NuGet package, which has its own associated packages, which are downloaded but never used. If you want to delete any of them, remember to keep *Microsoft.Azure.KeyVault.Core.dll*.

## Connection Configuration File

For the **AzureBlobStorageSync** `connections.config` file, you can add your credentials to a Blob Storage account. The format of this file is as follows:

    <?xml version="1.0" encoding="utf-8"?>
    <connectionStrings>
        <clear /> // Clears any other connectionStrings in the app.config file
        <add name="RemoteStorageConnectionString" connectionString="DefaultEndpointsProtocol=https;AccountName=<storageAccount>;AccountKey=<redacted>" />
    </connectionStrings>

This connection string can be copied and pasted from the [Azure Portal](https://portal.azure.com), from the **Primary Connection String** for your storage account.

For the **FileStorageSync** `connections.config` file, the format is as follows:

    <?xml version="1.0" encoding="utf-8"?\>
    <connectionStrings\>
        <clear /\> // Clears any other connectionStrings in the app.config file
        <add name="FileStorageUserAccount" connectionString="username"/\>
        <add name="FileStoragePassword" connectionString ="password"/\>
    </connectionStrings\>
