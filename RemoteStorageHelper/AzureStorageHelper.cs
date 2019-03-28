using System;
using System.Collections.Generic;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace RemoteStorageHelper
{
	public class AzureStorageHelper
	{
		public string ContainerName { get; private set; }
		public CloudStorageAccount StorageAccount { get; private set; }

		public AzureStorageHelper()
		{
			var connectionString = ConfigHelper.GetRemoteStorageConnectionString();
			StorageAccount = CloudStorageAccount.Parse(connectionString);
			ContainerName = ConfigHelper.GetConfigurationValue("container");
		}

		/// <summary>
		/// Parses the Cloud Storage account for a list of files to restore.
		/// </summary>
		/// <returns></returns>
		public List<RemoteItem> GetBlobItems()
		{
			Console.WriteLine("Fetching list of items in Remote Storage ...");
			var blobClient = StorageAccount.CreateCloudBlobClient();

			var container = blobClient.GetContainerReference(ContainerName);

			var blobItems = new List<RemoteItem>();

			// Loop over items within the container and fetch the blob files
			foreach (var item in container.ListBlobs(null, true))
			{
			    if (item is CloudBlockBlob blockBlob)
				{
					var blob = blockBlob;

					if (blob.Name.Length >= 15)
					{
						var blobItem = new RemoteItem
						{
							Name = blob.Name,
							BackupDate = Common.ParseDateFromFileName(blob.Name),
							BackupType = Common.ParseBackupTypeFromFileName(blob.Name),
							PathOrUrl = blob.Uri.ToString(),
							Size = blob.Properties.Length,
							LastModified = blob.Properties.LastModified,
							Type = ItemType.Block
						};
						blobItems.Add(blobItem);
					}
				}
				else
				{
				    if (!(item is CloudPageBlob pageBlob)) continue;
					var blob = pageBlob;

					if (blob.Name.Length >= 15)
					{
						var blobItem = new RemoteItem
						{
							Name = blob.Name,
							BackupDate = Common.ParseDateFromFileName(blob.Name),
							BackupType = Common.ParseBackupTypeFromFileName(blob.Name),
							PathOrUrl = blob.Uri.ToString(),
							Size = blob.Properties.Length,
							LastModified = blob.Properties.LastModified,
							Type = ItemType.Page
						};
						blobItems.Add(blobItem);
					}
				}
				Console.Write("\rRetrieved {0} item(s) ...", blobItems.Count);
			}
			Console.WriteLine();
			return blobItems;
		}
	}
}
