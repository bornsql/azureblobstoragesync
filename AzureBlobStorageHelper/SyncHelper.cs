using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AzureBlobStorageHelper
{
	public class SyncHelper
	{
		private readonly bool m_isDebug;
		private readonly DirectoryInfo m_localDirectory;
		private readonly AzureHelper m_azureHelper;
		private readonly bool m_deleteFilesFromAzure = false;

		/// <summary>
		/// Initializes a new instance of the <see cref="SyncHelper"/> class.
		/// </summary>
		/// <param name="debug">if set to <c>true</c> [debug].</param>
		public SyncHelper(bool debug)
		{
			m_azureHelper = new AzureHelper();
			m_isDebug = debug;

			// Get the application settings
			m_localDirectory = new DirectoryInfo(ConfigHelper.GetConfigurationValue("LocalPath"));
			m_deleteFilesFromAzure = ConfigHelper.GetConfigurationValue("DeleteFilesFromAzure")
				.Equals("True", StringComparison.InvariantCultureIgnoreCase);

		}

		/// <summary>
		/// Copies the files to Azure.
		/// </summary>
		/// <param name="filesToCopy">The files to copy.</param>
		private void CopyFilesToAzure(IEnumerable<string> filesToCopy)
		{
			var blobClient = m_azureHelper.StorageAccount.CreateCloudBlobClient();
			var container = blobClient.GetContainerReference(m_azureHelper.ContainerName);

			foreach (var file in filesToCopy)
			{
				// Retrieve reference to a blob named "myblob".
				var blockBlob = container.GetBlockBlobReference(file.Replace(@"\", @"/"));

				var localFilePath = string.Format(@"{0}\{1}", m_localDirectory, file);

				var fi = new FileInfo(localFilePath);
				if (!fi.Exists) continue;
				// Create or overwrite the blob with contents from local file
				using (var fileStream = File.OpenRead(@fi.FullName))
				{
					if (!m_isDebug)
					{
						Console.WriteLine("Uploading file [{0}]", file);
						blockBlob.UploadFromStream(fileStream);
					}
					else
					{
						Console.WriteLine("DEBUG: Uploading file [{0}]", file);
					}
				}
			}
		}

		/// <summary>
		/// Deletes the files from Azure.
		/// </summary>
		/// <param name="filesToDeleteOnAzure">The files to delete from Azure.</param>
		private void DeleteFilesFromAzure(IEnumerable<string> filesToDeleteOnAzure)
		{
			var blobClient = m_azureHelper.StorageAccount.CreateCloudBlobClient();
			var container = blobClient.GetContainerReference(m_azureHelper.ContainerName);

			foreach (var file in filesToDeleteOnAzure)
			{
				// Retrieve reference to a blob named "myblob".
				var blockBlob = container.GetBlockBlobReference(file.Replace(@"\", @"/"));

				if (!m_isDebug)
				{
					Console.WriteLine("Deleting file [{0}]", file);
					blockBlob.DeleteIfExists();
				}
				else
				{
					Console.WriteLine("DEBUG: Deleting file [{0}]", file);
				}
			}
		}

		/// <summary>
		/// Gets the local files.
		/// </summary>
		/// <returns></returns>
		// ReSharper disable once UnusedMember.Local
		private List<string> GetLocalFiles()
		{
			var localFiles = new List<string>();

			// Go see what's happening on the local directory first
			// This will be useful if we need to download files later
			Console.WriteLine("Fetching list of files on local machine ...");
			if (!m_localDirectory.Exists)
			{
				return localFiles;
			}

			var localPath = string.Format(@"{0}\", m_localDirectory);
			var files = Directory.GetFiles(m_localDirectory.ToString(), "*.*", SearchOption.AllDirectories);
			localFiles = files.Select(file => file.Replace(localPath, string.Empty)).ToList();

			return localFiles;
		}

		// This is the Sync bit
		/// <summary>
		/// Synchronizes between local path and Azure Blob Storage.
		/// </summary>
		public void Sync()
		{
			var azureHelper = new AzureHelper();

			var localFiles = GetLocalFiles();

			var blobItems = azureHelper.GetBlobItems();

			var syncHelper = new SyncHelper(m_isDebug);

			Console.WriteLine("Starting file comparison ...");
			var azureFiles = blobItems.Select(a => a.Name.Replace(@"/", @"\")).ToList();

			var filesToDeleteOnAzure = new List<string>(blobItems.Count);
			var filesToCopyToAzure = new List<string>(localFiles.Count);

			// Exists on Azure, does not exist on local - delete from Azure
			filesToDeleteOnAzure.AddRange(azureFiles.Where(azureFile => !localFiles.Contains(azureFile)));

			// Exists on local, does not exist on Azure - copy to Azure
			filesToCopyToAzure.AddRange(localFiles.Where(localFile => !azureFiles.Contains(localFile)));

			if (m_isDebug)
			{
				return;
			}
			syncHelper.CopyFilesToAzure(filesToCopyToAzure);

			// Now only explicitly delete files from Azure Blob Storage if this setting is set
			if (m_deleteFilesFromAzure)
			{
				syncHelper.DeleteFilesFromAzure(filesToDeleteOnAzure);
			}
			else
			{
				Console.WriteLine("No files will be deleted from Azure Blob Storage.");
			}
		}
	}
}