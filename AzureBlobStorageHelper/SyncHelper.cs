using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AzureBlobStorageHelper
{
	public class SyncHelper
	{
		private readonly AzureHelper m_azureHelper;
		private readonly bool m_copyFilesToAzure;
		private readonly bool m_deleteExplicitFilesFromAzure;
		private readonly bool m_deleteMissingFilesFromAzure;
		private readonly bool m_downloadFilesFromAzure;
		private readonly bool m_isDebug;
		private readonly DirectoryInfo m_localDirectory;
		private readonly string m_explicitFilesToDeleteMatchingString;

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

			m_copyFilesToAzure = ConfigHelper.GetConfigurationValue("CopyFilesToAzure")
				.Equals("True", StringComparison.InvariantCultureIgnoreCase);
			m_deleteExplicitFilesFromAzure = ConfigHelper.GetConfigurationValue("DeleteExplicitFilesFromAzure")
				.Equals("True", StringComparison.InvariantCultureIgnoreCase);
			m_deleteMissingFilesFromAzure = ConfigHelper.GetConfigurationValue("DeleteMissingFilesFromAzure")
				.Equals("True", StringComparison.InvariantCultureIgnoreCase);
			m_downloadFilesFromAzure = ConfigHelper.GetConfigurationValue("DownloadFilesFromAzure")
				.Equals("True", StringComparison.InvariantCultureIgnoreCase);
			m_explicitFilesToDeleteMatchingString = ConfigHelper.GetConfigurationValue("ExplicitFilesToDeleteMatchingString");

			if (string.IsNullOrEmpty(m_explicitFilesToDeleteMatchingString))
			{
				// Don't delete any explicit files from Azure
				m_deleteExplicitFilesFromAzure = false;
				m_explicitFilesToDeleteMatchingString = string.Empty;
			}

			// Override the debug value
			m_isDebug = !ConfigHelper.GetConfigurationValue("DebugOverride")
				.Equals("True", StringComparison.InvariantCultureIgnoreCase);
		}

		/// <summary>
		/// Copies the files to Azure.
		/// </summary>
		/// <param name="filesToCopy">The files to copy.</param>
		private void CopyFilesToAzure(IEnumerable<FileInfo> filesToCopy)
		{
			var blobClient = m_azureHelper.StorageAccount.CreateCloudBlobClient();
			var container = blobClient.GetContainerReference(m_azureHelper.ContainerName);

			foreach (var file in filesToCopy)
			{
				// Retrieve reference to a blob named "myblob".
				var blockBlob = container.GetBlockBlobReference(file.Name.Replace(@"\", @"/"));

				if (!file.Exists) continue;
				// Create or overwrite the blob with contents from local file
				using (var fileStream = File.OpenRead(@file.FullName))
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
					Console.Write("Deleting file [{0}]", file);
					var x = blockBlob.DeleteIfExists();
					Console.Write(x ? " - success\n" : "- failed\n");
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
		private Dictionary<FileInfo, string> GetLocalFiles()
		{
			var localFiles = new Dictionary<FileInfo, string>();

			Console.WriteLine("Fetching list of files on local machine ...");
			if (!m_localDirectory.Exists)
			{
				return localFiles;
			}

			foreach (var file in m_localDirectory.GetFiles("*.*", SearchOption.AllDirectories))
			{
				localFiles.Add(file, file.FullName.Replace($@"{m_localDirectory.FullName}\", string.Empty));
			}

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
			// var localFileNames = localFiles.Select(localFile => localFile.FullName.Replace(m_localDirectory.Name, string.Empty)).ToList();

			var blobItems = azureHelper.GetBlobItems();

			var syncHelper = new SyncHelper(m_isDebug);

			if (m_deleteExplicitFilesFromAzure && !string.IsNullOrEmpty(m_explicitFilesToDeleteMatchingString))
			{
				// Now only explicitly delete files from Azure Blob Storage if this setting is set
				var delete = blobItems.Where(x => x.Name.Contains(m_explicitFilesToDeleteMatchingString)).ToList();

				if (delete.Count > 0)
				{
					syncHelper.DeleteFilesFromAzure(delete.Select(d => d.Name));
					// Get new blobItems list
					blobItems = azureHelper.GetBlobItems();
				}
			}

			var downloadHelper = new DownloadHelper();

			Console.WriteLine("Starting file comparison ...");

			var filesToDeleteOnAzure = new List<string>(blobItems.Count);
			var filesToCopyToAzure = new List<FileInfo>(localFiles.Count);
			var filesToCopyFromAzure = new List<BlobItem>(blobItems.Count);

			// Exists on Azure, does not exist on local - download from Azure
			foreach (var azureFile in blobItems)
			{
				var localMatch = azureFile.Name.Replace(@"/", @"\");

				var localFile = localFiles.FirstOrDefault(x => x.Value == localMatch);

				if (localFile.Key != null && localFile.Value != null && localFile.Value.Contains(localMatch))
				{
					if (localFile.Key.Length != azureFile.Size)
					{
						filesToCopyFromAzure.Add(azureFile);
					}
				}
				else
				{
					filesToCopyFromAzure.Add(azureFile);
				}
			}

			// Exists on Azure, does not exist on local - delete from Azure
			filesToDeleteOnAzure.AddRange(from azureFile in blobItems where !localFiles.ContainsValue(azureFile.Name.Replace(@"/", @"\")) select azureFile.Name);

			// Exists on local, does not exist on Azure - copy to Azure
			filesToCopyToAzure.AddRange(from localFile in localFiles where !blobItems.Exists(x => x.Name.Replace(@"/", @"\") == localFile.Value) select localFile.Key);

			if (m_isDebug)
			{
				return;
			}

			if (m_copyFilesToAzure)
			{
				syncHelper.CopyFilesToAzure(filesToCopyToAzure);
			}

			if (m_downloadFilesFromAzure)
			{
				downloadHelper.DownloadFilesFromAzure(filesToCopyFromAzure);
			}

			if (m_deleteMissingFilesFromAzure)
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