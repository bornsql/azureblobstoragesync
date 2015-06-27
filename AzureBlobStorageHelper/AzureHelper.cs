using System;
using System.Collections.Generic;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;

namespace AzureBlobStorageHelper
{
	public class AzureHelper
	{
		public string ContainerName { get; private set; }
		public CloudStorageAccount StorageAccount { get; private set; }

		public AzureHelper()
		{
			var connectionString = ConfigHelper.GetAzureConnectionString();
			StorageAccount = CloudStorageAccount.Parse(connectionString);
			ContainerName = ConfigHelper.GetConfigurationValue("container");
		}

		/// <summary>
		/// Parses the Cloud Storage account for a list of files to restore.
		/// </summary>
		/// <returns></returns>
		public List<BlobItem> GetBlobItems()
		{
			Console.WriteLine("Fetching list of items in Azure Blob Storage ...");
			var blobClient = StorageAccount.CreateCloudBlobClient();

			var container = blobClient.GetContainerReference(ContainerName);

			var blobItems = new List<BlobItem>();

			// Loop over items within the container and fetch the blob files
			foreach (var item in container.ListBlobs(null, true))
			{
				var blockBlob = item as CloudBlockBlob;
				if (blockBlob != null)
				{
					var blob = blockBlob;

					var blobItem = new BlobItem
					{
						Name = blob.Name,
						BackupDate = ParseDateFromFileName(blob.Name),
						BackupType = ParseBackupTypeFromFileName(blob.Name),
						URL = blob.Uri.ToString(),
						Size = blob.Properties.Length,
						LastModified = blob.Properties.LastModified,
						Type = BlobItem.BlobType.Block
					};
					blobItems.Add(blobItem);
				}
				else
				{
					var pageBlob = item as CloudPageBlob;
					if (pageBlob == null) continue;
					var blob = pageBlob;

					var blobItem = new BlobItem
					{
						Name = blob.Name,
						BackupDate = ParseDateFromFileName(blob.Name),
						BackupType = ParseBackupTypeFromFileName(blob.Name),
						URL = blob.Uri.ToString(),
						Size = blob.Properties.Length,
						LastModified = blob.Properties.LastModified,
						Type = BlobItem.BlobType.Page
					};
					blobItems.Add(blobItem);
				}
			}
			return blobItems;
		}

		/// <summary>
		/// Parses the backup type from the file name.
		/// </summary>
		/// <param name="name">The file name.</param>
		/// <returns></returns>
		private static Backup ParseBackupTypeFromFileName(string name)
		{
			// We know nothing about this item name.
			// Hopefully it follows Ola Hallengren's naming convention, so let's start there

			// JUPITER$SQL2012_master_FULL_20140114_163121.bak
			var method = string.Empty;

			var splitter = name.Split('/'); // split out the file name from the Azure directory structure

			var filename = splitter.Length == 1 ? splitter[0] : splitter[splitter.Length - 1];

			if (splitter.Length > 1) // hack out the backup type from the directory name
			{
				method = splitter[1].ToUpperInvariant();
			}
			else
			{
				// Could just return the types here, but no accounting for Unknown
				if (filename.Contains("_FULL_") && !filename.Contains("_FULL_COPY_ONLY_"))
				{
					method = "FULL";
				}
				else if (filename.Contains("_FULL_COPY_ONLY_"))
				{
					method = "FULL_COPY_ONLY";
				}
				else if (filename.Contains("_DIFF_"))
				{
					method = "DIFF";
				}
				else if (filename.Contains("_LOG_"))
				{
					method = "LOG";
				}
			}

			switch (method)
			{
				case "FULL":
					return Backup.Full;
				case "FULL_COPY_ONLY":
					return Backup.CopyOnly;
				case "DIFF":
					return Backup.Differential;
				case "LOG":
					return Backup.TransactionLog;
				default:
					return Backup.Unknown;
			}
		}

		/// <summary>
		/// Parses the name of the date from the file name.
		/// </summary>
		/// <param name="name">The file name.</param>
		/// <returns></returns>
		private static DateTime? ParseDateFromFileName(string name)
		{
			// We know nothing about this item name.
			// Hopefully it follows Ola Hallengren's naming convention, so let's start there

			// JUPITER$SQL2012_master_FULL_20140114_163121.bak

			var splitter = name.Split('/'); // split out the file name from the Azure directory structure

			var filename = splitter.Length == 1 ? splitter[0] : splitter[splitter.Length - 1];

			if (!(filename.EndsWith(".bak") || filename.EndsWith(".trn")))
			{
				return null;
			}

			// Reverse the string to remove the extension and extract the date
			// Then reverse it back to parse the date
			var c = filename.ToCharArray();
			Array.Reverse(c);
			c = new string(c).Substring(4, 15).ToCharArray();
			Array.Reverse(c);
			var s = new string(c);

			return RestoreHelper.ParseDate(s);
		}
	}
}
