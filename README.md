# What Is It?

Azure Blob Storage Backup is a feature built right into SQL Server, but it is only available in SQL Server 2012 Service Pack 1 CU 2 and higher.

Furthermore, prior to SQL Server 2016, there is no way to back up to more than one location simultaneously. You have to choose between local backups or Azure Blob Storage. It is not possible to pick both. This is especially problematic if you perform Differential Backups.

**AzureBlobStorageSync** is a free tool that allows you to continue backing up your database locally, for any version of SQL Server, and synchronise your files to Azure Blob Storage on a schedule of your choosing.

**FileStorageSync** allows you to synchronise your files to a UNC path on a schedule of your choosing.

It can work alongside your existing backup process, and leverages [Ola Hallengren's][1] Maintenance Solution.

There is a companion restore tool, **AzureBlobStorageRestore** (and **FileStorageRestore**), also free, which is able to download the latest database (including Full, Differential and Transaction Log Backups), only knowing the name of the database, and build a restore script. This is especially useful if you suffer a catastrophic failure and have no other knowledge of the backups (e.g. the content of the *msdb* database).

### How Do I Do It?

Run this tool on any machine that can access your SQL Server backup files using UNC, or the SQL Server instance itself.

The AzureBlobStorageSync and Restore requires:
- Azure Blob Storage subscription
- .NET Core 3.1 LTS
- Internet access!

The FileStorageSync and Restore requires:
- .NET Core 3.1 LTS

The Sync and Restore components have a dependency on the *Azure.Storage.Blobs* NuGet package.

### Connection Configuration File

For the **AzureBlobStorageSync** `connections.json` file, you can add your credentials to a Blob Storage account. The file is in JSON format, as follows:

	{
	    "RemoteStorageConnectionString": "DefaultEndpointsProtocol=https;AccountName=<storageAccount>;AccountKey=<redacted>"
	}

This connection string can be copied and pasted from the [Azure Portal][2], from the **Primary Connection String** for your storage account.

For the **FileStorageSync** `connections.json` file, the format is as follows:

	{
	    "FileStorageUserAccount": "username",
	    "FileStoragePassword": "password"
	}

[1]:	https://ola.hallengren.com/
[2]:	https://portal.azure.com