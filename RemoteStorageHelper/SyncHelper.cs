using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

namespace RemoteStorageHelper
{
	public class SyncHelper
	{
		// Generic
		private readonly bool m_copyFilesToRemoteStorage;
		private readonly bool m_deleteExplicitFilesFromRemoteStorage;
		private readonly bool m_deleteMissingFilesFromRemoteStorage;
		private readonly bool m_fetchExplicitFilesFromRemoteStorage;
		private readonly bool m_fetchFilesFromRemoteStorage;
		private readonly bool m_isDebug;
		private readonly bool m_syncEncryptedFilesOnly;
		private readonly string m_encryptedFileExtension;
		private readonly string m_explicitFilesToDeleteMatchingString;
		private readonly string m_explicitFilesToFetchMatchingString;
		private readonly Common m_common;
		private readonly DirectoryInfo m_localDirectory;
		private readonly ItemClass m_remoteItemClass;
		private readonly ItemSortOrder m_itemSortOrder;

		// Azure-specific
		private readonly AzureStorageHelper m_azureStorageHelper;

		// File-specific
		private readonly FileStorageHelper m_fileStorageHelper;
		private readonly DirectoryInfo m_remoteStorage;
		private readonly string m_remoteUsername;
		private readonly string m_remotePassword;

		public SyncHelper(ItemClass itemClass, bool debug)
		{
			m_remoteItemClass = itemClass;
			m_common = new Common();

			m_isDebug = debug;

			if (m_remoteItemClass == ItemClass.Blob)
			{
				m_azureStorageHelper = m_azureStorageHelper ?? new AzureStorageHelper();
			}
			else
			{
				m_fileStorageHelper = m_fileStorageHelper ?? new FileStorageHelper();
				m_remoteStorage = new DirectoryInfo(ConfigHelper.GetConfigurationValue("RemoteStorage"));
				m_remoteUsername = ConfigHelper.GetFileStorageUsername();
				m_remotePassword = ConfigHelper.GetFileStoragePassword();
			}

			// Get the application settings
			m_localDirectory = new DirectoryInfo(ConfigHelper.GetConfigurationValue("LocalPath"));
			m_remoteStorage = new DirectoryInfo(ConfigHelper.GetConfigurationValue("RemoteStorage"));

			m_copyFilesToRemoteStorage = ConfigHelper.GetConfigurationBoolean("CopyFilesToRemoteStorage");
			m_deleteExplicitFilesFromRemoteStorage = ConfigHelper.GetConfigurationBoolean("DeleteExplicitFilesFromRemoteStorage");
			m_fetchExplicitFilesFromRemoteStorage = ConfigHelper.GetConfigurationBoolean("FetchExplicitFilesFromRemoteStorage");
			m_deleteMissingFilesFromRemoteStorage = ConfigHelper.GetConfigurationBoolean("DeleteMissingFilesFromRemoteStorage");
			m_fetchFilesFromRemoteStorage = ConfigHelper.GetConfigurationBoolean("FetchFilesFromRemoteStorage");
			m_explicitFilesToDeleteMatchingString = ConfigHelper.GetConfigurationValue("ExplicitFilesToDeleteMatchingString");
			m_explicitFilesToFetchMatchingString = ConfigHelper.GetConfigurationValue("ExplicitFilesToFetchMatchingString");
			m_itemSortOrder = ConfigHelper.GetConfigurationValue("ItemSortOrder")
				.Equals("Size", StringComparison.InvariantCultureIgnoreCase)
				? ItemSortOrder.Size
				: ItemSortOrder.Name;
			var compressAndEncrypt = ConfigHelper.GetConfigurationBoolean("CompressAndEncrypt");
			var encryptOnly = ConfigHelper.GetConfigurationBoolean("EncryptOnly");


			if (string.IsNullOrEmpty(m_explicitFilesToDeleteMatchingString))
			{
				// Don't delete any explicit files from Remote Path
				m_deleteExplicitFilesFromRemoteStorage = false;
				m_explicitFilesToDeleteMatchingString = string.Empty;
			}

			if (string.IsNullOrEmpty(m_explicitFilesToFetchMatchingString))
			{
				// Don't delete any explicit files from Remote Path
				m_fetchExplicitFilesFromRemoteStorage = false;
				m_explicitFilesToFetchMatchingString = string.Empty;
			}

			// Override the debug value
			m_isDebug = !ConfigHelper.GetConfigurationValue("DebugOverride")
				.Equals("True", StringComparison.InvariantCultureIgnoreCase);

			// Encrypt and Compress settings -- only one can be true, default to EncryptOnly if set
			if (compressAndEncrypt && encryptOnly)
			{
				compressAndEncrypt = false;
			}

			// Will only synchronise files that have the EncryptedFileExtension file type
			// And this is only set if either of the encryption settings is set
			m_syncEncryptedFilesOnly = compressAndEncrypt || encryptOnly;
			m_encryptedFileExtension = ConfigHelper.GetConfigurationValue("EncryptedFileExtension") ?? ".absz";

		}

		private void CopyToFileStorage(IEnumerable<FileInfo> filesToCopy)
		{
			using (new NetworkConnection(m_remoteStorage.FullName, new NetworkCredential(m_remoteUsername, m_remotePassword)))
			{
				var container = m_common.GetFileList("*.*", m_remoteStorage.FullName).ToList();

				var copyFile = false;
				string remoteDirectory;

				foreach (var file in filesToCopy)
				{
					var remoteFile = container.FirstOrDefault(fi => fi.Name == file.Name);
					if (file.DirectoryName == null)
					{
						continue;
					}

					var remoteFileName = file.FullName.Replace(m_localDirectory.FullName, m_remoteStorage.FullName);

					if (remoteFile != null && remoteFile.Exists)
					{
						// If files are not the same size and date, overwrite
						if (remoteFile.Length != file.Length || remoteFile.LastWriteTimeUtc != file.LastWriteTimeUtc)
						{
							copyFile = true;
						}
					}
					else
					{
						copyFile = true;
					}

					if (!file.Exists)
					{
						continue;
					}

					// Skip the file if it doesn't match the file extension
					if (m_syncEncryptedFilesOnly && file.Extension != m_encryptedFileExtension)
					{
						continue;
					}

					if (!copyFile)
					{
						continue;
					}

					Console.WriteLine("Copying file [{0}]", file);
					if (remoteFile != null && remoteFile.Exists)
					{
						remoteFile.Delete();
					}

					// Make sure target directory exists
					remoteDirectory = remoteFileName.Replace(file.Name, string.Empty);

					if (!Directory.Exists(remoteDirectory))
					{
						Directory.CreateDirectory(remoteDirectory);
					}

					File.Copy(file.FullName, remoteFileName);
				}
			}
		}

		private void CopyToAzureStorage(IEnumerable<FileInfo> filesToCopy)
		{
			var blobClient = m_azureStorageHelper.StorageAccount.CreateCloudBlobClient();
			var container = blobClient.GetContainerReference(m_azureStorageHelper.ContainerName);

			foreach (var file in filesToCopy)
			{
				// Retrieve reference to a blob named "myblob".
				var blockBlob = container.GetBlockBlobReference(file.Name.Replace(@"\", @"/"));

				if (!file.Exists)
				{
					continue;
				}

				// Skip the file if it doesn't match the file extension
				if (m_syncEncryptedFilesOnly && file.Extension != m_encryptedFileExtension)
				{
					continue;
				}

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

		private void DeleteFromFileStorage(IEnumerable<RemoteItem> filesToDeleteRemotely, bool encryptedFilesOnly)
		{
			var container = m_remoteStorage.GetFiles().ToList();

			foreach (var file in filesToDeleteRemotely)
			{
				// Retrieve reference to files
				if (encryptedFilesOnly && !file.Name.EndsWith(m_encryptedFileExtension, StringComparison.InvariantCultureIgnoreCase))
				{
					Console.Write("Skipping non-encrypted file [{0}]", file);
					continue;
				}

				var remoteFile = container.First(fi => fi.Name == file.Name);

				if (!m_isDebug)
				{
					try
					{
						Console.Write("Deleting file [{0}]", file);
						if (File.Exists(remoteFile.FullName))
						{
							remoteFile.Delete();
							Console.Write(" - success\n");
						}
					}
					catch (Exception)
					{
						Console.Write(" - failed\n");
					}
				}
				else
				{
					Console.WriteLine("DEBUG: Deleting file [{0}]", file);
				}
			}
		}

		private void DeleteFromAzureStorage(IEnumerable<RemoteItem> filesToDeleteRemotely, bool encryptedFilesOnly)
		{
			var blobClient = m_azureStorageHelper.StorageAccount.CreateCloudBlobClient();
			var container = blobClient.GetContainerReference(m_azureStorageHelper.ContainerName);

			foreach (var file in filesToDeleteRemotely)
			{
				// Retrieve reference to blobs
				if (encryptedFilesOnly && !file.Name.EndsWith(m_encryptedFileExtension, StringComparison.InvariantCultureIgnoreCase))
				{
					Console.Write("Skipping non-encrypted file [{0}]", file);
					continue;
				}

				var blockBlob = container.GetBlockBlobReference(file.Name.Replace(@"\", @"/"));

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
		/// <returns>List of FileInfo</returns>
		private List<FileInfo> GetLocalFiles(bool encryptedFilesOnly)
		{
			var localFiles = new List<FileInfo>();

			Console.WriteLine("Fetching list of files on local machine ...");
			if (!m_localDirectory.Exists)
			{
				return localFiles;
			}

			var files = m_common.GetFileList(encryptedFilesOnly ? $"*{m_encryptedFileExtension}" : "*.*", m_localDirectory.FullName);

			localFiles.AddRange(files);

			return localFiles;
		}

		// Synchronises with Remote File Storage
		public void SyncFileStorage()
		{
			var fileHelper = new FileStorageHelper();

			var localFiles = GetLocalFiles(m_syncEncryptedFilesOnly);
			// var localFileNames = localFiles.Select(localFile => localFile.FullName.Replace(m_localDirectory.Name, string.Empty)).ToList();

			var remoteFiles = fileHelper.GetFileItems();

			var syncHelper = new SyncHelper(m_remoteItemClass, m_isDebug);

			if (m_deleteExplicitFilesFromRemoteStorage && !string.IsNullOrEmpty(m_explicitFilesToDeleteMatchingString))
			{
				// Now only explicitly delete files from Remote File Storage if this setting is set
				var delete = remoteFiles.Where(x => x.Name.Contains(m_explicitFilesToDeleteMatchingString)).ToList();

				if (delete.Count > 0)
				{
					syncHelper.DeleteFromFileStorage(delete, m_syncEncryptedFilesOnly);
					// Get new FileInfos list
					remoteFiles = fileHelper.GetFileItems();
				}
			}

			var remoteFetchHelper = new RemoteFetchHelper(m_remoteItemClass);

			Console.WriteLine("Starting file comparison ...");

			var filesToDeleteRemotely = new List<RemoteItem>(remoteFiles.Count);
			var filesToCopyToRemoteStorage = new List<FileInfo>(localFiles.Count);
			var filesToCopyFromRemoteStorage = new List<RemoteItem>(remoteFiles.Count);

			// Exists on Remote Path, does not exist on local - fetch from Remote Path
			foreach (var remoteFile in remoteFiles)
			{
				var localMatch = remoteFile.Name;

				var localFile = localFiles.FirstOrDefault(x => x.Name == localMatch);

				if (localFile != null && localFile.Name.Contains(localMatch))
				{
					if (localFile.Length != remoteFile.Size)
					{
						filesToCopyFromRemoteStorage.Add(remoteFile);
					}
				}
				else
				{
					filesToCopyFromRemoteStorage.Add(remoteFile);
				}
			}

			// Exists on File Path, does not exist on local - delete from File Path
			filesToDeleteRemotely.AddRange(from remoteFile in remoteFiles
										   where !localFiles.Exists(x => x.Name == remoteFile.Name)
										   select remoteFile);

			// Exists on local, does not exist on File Path - copy to File Path
			filesToCopyToRemoteStorage.AddRange(from localFile in localFiles
												where !remoteFiles.Exists(x => x.Name == localFile.Name)
												select localFile);

			if (m_copyFilesToRemoteStorage)
			{
				syncHelper.CopyToFileStorage(filesToCopyToRemoteStorage);
			}

			if (m_fetchFilesFromRemoteStorage)
			{
				// If explicit matching string is set for files to fetch,
				// then fetch those only, otherwise fetch everything
				remoteFetchHelper.FetchItemsFromRemoteStorage(
					m_fetchExplicitFilesFromRemoteStorage
						? filesToCopyFromRemoteStorage.Where(remoteFile => remoteFile.Name.Contains(m_explicitFilesToFetchMatchingString))
						: filesToCopyFromRemoteStorage, m_itemSortOrder);
			}

			if (m_deleteMissingFilesFromRemoteStorage)
			{
				syncHelper.DeleteFromFileStorage(filesToDeleteRemotely, m_syncEncryptedFilesOnly);
			}
			else
			{
				Console.WriteLine("No files will be deleted from Remote File Storage.");
			}
		}

		// Synchronises with Azure Blob Storage
		public void SyncAzureStorage()
		{
			var common = new Common();

			var localFiles = common.GetLocalFiles(m_syncEncryptedFilesOnly);

			var blobItems = m_azureStorageHelper.GetBlobItems();

			var syncHelper = new SyncHelper(m_remoteItemClass, m_isDebug);

			if (m_deleteExplicitFilesFromRemoteStorage && !string.IsNullOrEmpty(m_explicitFilesToDeleteMatchingString))
			{
				// Now only explicitly delete files from Remote Storage if this setting is set
				var delete = blobItems.Where(x => x.Name.Contains(m_explicitFilesToDeleteMatchingString)).ToList();

				if (delete.Count > 0)
				{
					syncHelper.DeleteFromAzureStorage(delete.Select(d => d), m_syncEncryptedFilesOnly);
					// Get new blobItems list
					blobItems = m_azureStorageHelper.GetBlobItems();
				}
			}

			var remoteFetchHelper = new RemoteFetchHelper(m_remoteItemClass);

			Console.WriteLine("Starting file comparison ...");

			var filesToDeleteOnRemoteStorage = new List<RemoteItem>(blobItems.Count);
			var filesToCopyToRemoteStorage = new List<FileInfo>(localFiles.Count);
			var filesToCopyFromRemoteStorage = new List<RemoteItem>(blobItems.Count);

			// Exists on RemoteStorage, does not exist on local - fetch from Remote Storage
			foreach (var azureFile in blobItems)
			{
				var localMatch = azureFile.Name.Replace(@"/", @"\");

				var localFile = localFiles.FirstOrDefault(x => x.Value == localMatch);

				if (localFile.Key != null && localFile.Value != null && localFile.Value.Contains(localMatch))
				{
					if (localFile.Key.Length != azureFile.Size)
					{
						filesToCopyFromRemoteStorage.Add(azureFile);
					}
				}
				else
				{
					filesToCopyFromRemoteStorage.Add(azureFile);
				}
			}

			// Exists on RemoteStorage, does not exist on local - delete from RemoteStorage
			filesToDeleteOnRemoteStorage.AddRange(from azureFile in blobItems
												  where !localFiles.ContainsValue(azureFile.Name.Replace(@"/", @"\"))
												  select azureFile);

			// Exists on local, does not exist on RemoteStorage - copy to RemoteStorage
			filesToCopyToRemoteStorage.AddRange(from localFile in localFiles
												where !blobItems.Exists(x => x.Name.Replace(@"/", @"\") == localFile.Value)
												select localFile.Key);

			if (m_isDebug)
			{
				return;
			}

			if (m_copyFilesToRemoteStorage)
			{
				syncHelper.CopyToAzureStorage(filesToCopyToRemoteStorage);
			}

			if (m_fetchFilesFromRemoteStorage)
			{
				// If explicit matching string is set for files to fetch,
				// then fetch those only, otherwise fetch everything
				remoteFetchHelper.FetchItemsFromRemoteStorage(
					m_fetchExplicitFilesFromRemoteStorage
						? filesToCopyFromRemoteStorage.Where(blobItem => blobItem.Name.Contains(m_explicitFilesToFetchMatchingString))
						: filesToCopyFromRemoteStorage, m_itemSortOrder);
			}

			if (m_deleteMissingFilesFromRemoteStorage)
			{
				syncHelper.DeleteFromAzureStorage(filesToDeleteOnRemoteStorage, m_syncEncryptedFilesOnly);
			}
			else
			{
				Console.WriteLine("No files will be deleted from Remote Storage.");
			}
		}
	}
}

