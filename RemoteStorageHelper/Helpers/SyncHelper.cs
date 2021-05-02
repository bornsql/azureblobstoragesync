using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Azure.Storage.Blobs.Models;
using RemoteStorageHelper.Entities;
using RemoteStorageHelper.Enums;

namespace RemoteStorageHelper.Helpers
{
	public class SyncHelper
	{
		// Generic
		private readonly bool m_copyFilesToRemoteStorage;
		private readonly bool m_deleteExplicitFilesFromRemoteStorage;
		private readonly bool m_deleteMissingFilesFromRemoteStorage;
		private readonly bool m_deleteMissingFilesFromLocalStorage;
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
		private readonly bool m_isRunningOnWindows;

		// Azure-specific
		private readonly AzureStorageHelper m_azureStorageHelper;

		private List<RemoteItemEntity> m_blobItems;

		// File-specific
		private readonly FileStorageHelper m_fileStorageHelper;
		private readonly DirectoryInfo m_remoteStorage;
		private readonly string m_remoteUsername;
		private readonly string m_remotePassword;

		public SyncHelper(ItemClass itemClass, bool debug)
		{
			var connections = JsonWrangler.ReadJsonItem<ConnectionEntity>(new FileInfo("connections.json"));
			var restoreConfig = JsonWrangler.ReadJsonItem<RestoreConfigurationEntity>(new FileInfo("restore.json"));
			var syncConfig = JsonWrangler.ReadJsonItem<SyncConfigurationEntity>(new FileInfo("sync.json"));

			m_remoteItemClass = itemClass;
			m_common = new Common();

			m_isDebug = debug;

			if (m_remoteItemClass == ItemClass.Blob)
			{
				m_azureStorageHelper ??= new AzureStorageHelper(connections, syncConfig);
			}
			else
			{
				m_fileStorageHelper ??= new FileStorageHelper(restoreConfig, connections);
				m_remoteStorage = new DirectoryInfo(restoreConfig.RemoteStorage);
				m_remoteUsername = connections.FileStorageUserAccount;
				m_remotePassword = connections.FileStoragePassword;
			}

			m_isRunningOnWindows = Path.DirectorySeparatorChar.Equals('\\');

			// Get the application settings
			m_localDirectory = new DirectoryInfo(restoreConfig.LocalPath);
			m_remoteStorage = new DirectoryInfo(restoreConfig.RemoteStorage);

			m_copyFilesToRemoteStorage = restoreConfig.CopyFilesToRemoteStorage;
			m_deleteExplicitFilesFromRemoteStorage = syncConfig.DeleteExplicitFilesFromRemoteStorage;
			m_fetchExplicitFilesFromRemoteStorage = syncConfig.FetchExplicitFilesFromRemoteStorage;
			m_deleteMissingFilesFromRemoteStorage = syncConfig.DeleteMissingFilesFromRemoteStorage;
			m_deleteMissingFilesFromLocalStorage = syncConfig.DeleteMissingFilesFromLocalStorage;
			m_fetchFilesFromRemoteStorage = syncConfig.FetchFilesFromRemoteStorage;
			m_explicitFilesToDeleteMatchingString = syncConfig.ExplicitFilesToDeleteMatchingString;
			m_explicitFilesToFetchMatchingString = syncConfig.ExplicitFilesToFetchMatchingString;
			m_itemSortOrder = syncConfig.ItemSortOrder.Equals("Size", StringComparison.InvariantCultureIgnoreCase)
				? ItemSortOrder.Size
				: ItemSortOrder.Name;
			var compressAndEncrypt = syncConfig.CompressAndEncrypt;
			var encryptOnly = syncConfig.EncryptOnly;

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
			m_isDebug = syncConfig.DebugOverride;

			// Encrypt and Compress settings -- only one can be true, default to EncryptOnly if set
			if (compressAndEncrypt && encryptOnly)
			{
				compressAndEncrypt = false;
			}

			// Will only synchronise files that have the EncryptedFileExtension file type
			// And this is only set if either of the encryption settings is set
			m_syncEncryptedFilesOnly = compressAndEncrypt || encryptOnly;
			m_encryptedFileExtension = syncConfig.EncryptedFileExtension ?? ".absz";
		}

		private void CopyToFileStorage(IEnumerable<FileInfo> filesToCopy)
		{
			using (new NetworkConnection(m_remoteStorage.FullName,
				new NetworkCredential(m_remoteUsername, m_remotePassword)))
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

					if (remoteFile is {Exists: true})
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

					Console.WriteLine($"Copying file [{file}]");
					if (remoteFile is {Exists: true})
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

		private async Task CopyToAzureStorageAsync(IEnumerable<FileInfo> filesToCopy)
		{
			string fn;

			foreach (var file in filesToCopy)
			{
				// Get the file name
				fn = m_isRunningOnWindows
					? file.Name.Replace(@"\", @"/")
					: file.Name;

				if (!file.Exists)
				{
					continue;
				}

				// Skip the file if it doesn't match the file extension
				if (m_syncEncryptedFilesOnly && file.Extension != m_encryptedFileExtension)
				{
					continue;
				}

				// Create or overwrite the blob with contents from the local file
				if (!m_isDebug)
				{
					Console.WriteLine($"Uploading file [{file}]");
					// Retrieve reference to a blob named "fn".
					var client = m_azureStorageHelper.Container.GetBlobClient(fn);
					await client.UploadAsync(File.OpenRead(fn));
				}
				else
				{
					Console.WriteLine($"DEBUG: Uploading file [{file}]");
				}
			}
		}

		private void DeleteFromFileStorage(IEnumerable<RemoteItemEntity> filesToDeleteRemotely, bool encryptedFilesOnly)
		{
			var container = m_remoteStorage.GetFiles().ToList();

			foreach (var file in filesToDeleteRemotely)
			{
				// Retrieve reference to files
				if (encryptedFilesOnly &&
					!file.Name.EndsWith(m_encryptedFileExtension, StringComparison.InvariantCultureIgnoreCase))
				{
					Console.Write($"Skipping non-encrypted file [{file}]");
					continue;
				}

				var remoteFile = container.First(fi => fi.Name == file.Name);

				if (!m_isDebug)
				{
					try
					{
						Console.Write($"Deleting file [{file}]");
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
					Console.WriteLine($"DEBUG: Deleting file [{file}]");
				}
			}
		}

		private void DeleteFromLocalStorage(IEnumerable<FileInfo> filesToDeleteLocally, bool encryptedFilesOnly)
		{
			foreach (var file in filesToDeleteLocally)
			{
				if (File.Exists(file.FullName))
				{
					if (!m_isDebug)
					{
						Console.WriteLine($@"Deleting local file [{file}]");
						File.Delete(file.FullName);
					}
					else
					{
						Console.WriteLine($@"DEBUG: Deleting local file [{file}]");
					}
				}
			}
		}

		private void DeleteFromAzureStorage(IEnumerable<RemoteItemEntity> filesToDeleteRemotely, bool encryptedFilesOnly)
		{
			var blobClient = m_azureStorageHelper.ServiceClient;
			var container = m_azureStorageHelper.Container;

			foreach (var file in filesToDeleteRemotely)
			{
				// Retrieve reference to blobs
				if (encryptedFilesOnly &&
					!file.Name.EndsWith(m_encryptedFileExtension, StringComparison.InvariantCultureIgnoreCase))
				{
					Console.Write($"Skipping non-encrypted file [{file}]");
					continue;
				}

				var fn = m_isRunningOnWindows
					? file.Name.Replace(@"\", @"/")
					: file.Name;

				var item = container.GetBlobClient(fn);

				if (!m_isDebug)
				{
					Console.Write($"Deleting file [{file}]");
					var x = item.DeleteIfExists(DeleteSnapshotsOption.IncludeSnapshots);
					Console.Write(x
						? $" - success{Environment.NewLine}"
						: $"- failed{Environment.NewLine}");
				}
				else
				{
					Console.WriteLine($"DEBUG: Deleting file [{file}]");
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

			var files = m_common.GetFileList(encryptedFilesOnly
				? $"*{m_encryptedFileExtension}"
				: "*.*", m_localDirectory.FullName);

			localFiles.AddRange(files);

			return localFiles;
		}

		// Synchronises with Remote File Storage
		public void SyncFileStorage()
		{
			var restore = JsonWrangler.ReadJsonItem<RestoreConfigurationEntity>(new FileInfo("restore.json"));
			var connections = JsonWrangler.ReadJsonItem<ConnectionEntity>(new FileInfo("connections.json"));

			var fileHelper = new FileStorageHelper(restore, connections);

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

			var filesToDeleteRemotely = new List<RemoteItemEntity>(remoteFiles.Count);
			var filesToDeleteLocally = new List<FileInfo>(localFiles.Count);
			var filesToCopyToRemoteStorage = new List<FileInfo>(localFiles.Count);
			var filesToCopyFromRemoteStorage = new List<RemoteItemEntity>(remoteFiles.Count);

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
			filesToDeleteRemotely.AddRange(remoteFiles.Where(remoteFile =>
				!localFiles.Exists(x => x.Name == remoteFile.Name)));

			// Exists on local, does not exist on File Path - delete from local
			filesToDeleteLocally.AddRange(localFiles.Where(localFile =>
				!remoteFiles.Exists(x => x.Name == localFile.Name)));

			// Exists on local, does not exist on File Path - copy to File Path
			filesToCopyToRemoteStorage.AddRange(localFiles.Where(localFile =>
				!remoteFiles.Exists(x => x.Name == localFile.Name)));

			if (m_copyFilesToRemoteStorage)
			{
				syncHelper.CopyToFileStorage(filesToCopyToRemoteStorage);
			}

			if (m_fetchFilesFromRemoteStorage)
			{
				// If explicit matching string is set for files to fetch,
				// then fetch those only, otherwise fetch everything
				remoteFetchHelper.FetchItemsFromRemoteStorage(m_fetchExplicitFilesFromRemoteStorage
					? filesToCopyFromRemoteStorage.Where(remoteFile =>
						remoteFile.Name.Contains(m_explicitFilesToFetchMatchingString)).ToList()
					: filesToCopyFromRemoteStorage, m_itemSortOrder);
			}

			if (m_deleteMissingFilesFromLocalStorage)
			{
				syncHelper.DeleteFromLocalStorage(filesToDeleteLocally, m_syncEncryptedFilesOnly);
			}
			else
			{
				Console.WriteLine("No files will be deleted from Local File Storage.");
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
		public async Task SyncAzureStorageAsync()
		{
			var common = new Common();

			var localFiles = common.GetLocalFiles(m_syncEncryptedFilesOnly);

			m_blobItems = await m_azureStorageHelper.GetBlobItemListAsync();

			var syncHelper = new SyncHelper(m_remoteItemClass, m_isDebug);

			if (m_deleteExplicitFilesFromRemoteStorage && !string.IsNullOrEmpty(m_explicitFilesToDeleteMatchingString))
			{
				// Now only explicitly delete files from Remote Storage if this setting is set
				var delete = m_blobItems.Where(x => x.Name.Contains(m_explicitFilesToDeleteMatchingString)).ToList();

				if (delete.Count > 0)
				{
					syncHelper.DeleteFromAzureStorage(delete.Select(d => d), m_syncEncryptedFilesOnly);
					// Get new blobItems list
					m_blobItems = await m_azureStorageHelper.GetBlobItemListAsync();
				}
			}

			var remoteFetchHelper = new RemoteFetchHelper(m_remoteItemClass);

			Console.WriteLine("Starting file comparison ...");

			var filesToDeleteOnRemoteStorage = new List<RemoteItemEntity>(m_blobItems.Count);
			var filesToCopyToRemoteStorage = new List<FileInfo>(localFiles.Count);
			var filesToCopyFromRemoteStorage = new List<RemoteItemEntity>(m_blobItems.Count);

			// Exists on RemoteStorage, does not exist on local - fetch from Remote Storage
			foreach (var azureFile in m_blobItems)
			{
				var localMatch = m_isRunningOnWindows
					? azureFile.Name.Replace(@"\", @"/")
					: azureFile.Name;

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
			filesToDeleteOnRemoteStorage.AddRange(from azureFile in m_blobItems
												  where !localFiles.ContainsValue(m_isRunningOnWindows
													  ? azureFile.Name.Replace(@"\", @"/")
													  : azureFile.Name)
												  select azureFile);

			// Exists on local, does not exist on RemoteStorage - copy to RemoteStorage
			filesToCopyToRemoteStorage.AddRange(from localFile in localFiles
												where !m_blobItems.Exists(x => (m_isRunningOnWindows
													? x.Name.Replace(@"/", @"\")
													: x.Name) == localFile.Value)
												select localFile.Key);

			if (m_isDebug)
			{
				return;
			}

			if (m_copyFilesToRemoteStorage)
			{
				await syncHelper.CopyToAzureStorageAsync(filesToCopyToRemoteStorage);
			}

			if (m_fetchFilesFromRemoteStorage)
			{
				// If explicit matching string is set for files to fetch,
				// then fetch those only, otherwise fetch everything
				remoteFetchHelper.FetchItemsFromRemoteStorage(m_fetchExplicitFilesFromRemoteStorage
					? filesToCopyFromRemoteStorage.Where(blobItem =>
						blobItem.Name.Contains(m_explicitFilesToFetchMatchingString)).ToList()
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
