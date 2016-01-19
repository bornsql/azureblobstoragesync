using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AzureBlobStorageHelper
{
	public class DownloadHelper
	{
		private readonly AzureHelper m_azureHelper;
		private readonly string m_localDirectory;

		public DownloadHelper()
		{
			m_azureHelper = new AzureHelper();
			m_localDirectory = ConfigHelper.GetConfigurationValue("LocalPath");
		}

		/// <summary>
		/// Downloads the files from Azure.
		/// </summary>
		/// <param name="filesToDownload">The files to download.</param>
		/// <returns></returns>
		public Dictionary<FileInfo, string> DownloadFilesFromAzure(IEnumerable<BlobItem> filesToDownload)
		{
			var fileList = filesToDownload.ToList();
			var blobClient = m_azureHelper.StorageAccount.CreateCloudBlobClient();
			var container = blobClient.GetContainerReference(m_azureHelper.ContainerName);

			// Build list of files for restore script
			var files = fileList.ToDictionary(file => new FileInfo($@"{m_localDirectory}\{file.Name.Replace(@"/", @"\")}"),
				file => file.BackupType.ToFriendlyString());

			// Sort by smallest file first
			var sortedFilesToDownload = fileList.OrderBy(x => x.Size);

			foreach (var file in sortedFilesToDownload)
			{
				// Retrieve reference to a blob named "myblob".
				var blockBlob = container.GetBlockBlobReference(file.Name);

				var localFilePath = $@"{m_localDirectory}\{file.Name.Replace(@"/", @"\")}";

				var fi = new FileInfo(localFilePath);

				// Skip the file if it exists
				if (fi.Exists && fi.Length == file.Size)
				{
					SetFileCreationDate(file, fi);
					continue;
				}

				if (fi.DirectoryName != null)
				{
					var di = new DirectoryInfo(fi.DirectoryName);
					if (!di.Exists)
					{
						di.Create();
					}
				}

				using (var fileStream = File.Create(@fi.FullName))
				{
					Console.WriteLine("Downloading file [{0}]", file.Name);
					blockBlob.DownloadToStream(fileStream);
				}

				SetFileCreationDate(file, fi);
			}

			return files;
		}

		private static void SetFileCreationDate(BlobItem file, FileSystemInfo fi)
		{
			var dt = DateTime.UtcNow;

			if (file.BackupDate == null)
			{
				if (file.LastModified != null)
				{
					dt = file.LastModified.Value.UtcDateTime;
				}
			}
			else
			{
				dt = file.BackupDate.Value;
			}

			File.SetLastWriteTimeUtc(fi.FullName, dt);
		}
	}
}
