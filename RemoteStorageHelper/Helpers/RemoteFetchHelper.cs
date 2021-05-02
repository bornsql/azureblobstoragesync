using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RemoteStorageHelper.Entities;
using RemoteStorageHelper.Enums;

namespace RemoteStorageHelper.Helpers
{
	public class RemoteFetchHelper
	{
		private readonly AzureStorageHelper m_azureHelper;
		private readonly string m_localDirectory;
		private readonly ItemClass m_remoteItemClass;
		private readonly string m_remoteStorage;

		public RemoteFetchHelper(ItemClass itemClass)
		{
			m_remoteItemClass = itemClass;
			var config = JsonWrangler.ReadJsonItem<RestoreConfigurationEntity>(new FileInfo("restore.json"));
			var connections = JsonWrangler.ReadJsonItem<ConnectionEntity>(new FileInfo("connections.json"));

			m_localDirectory = config.LocalPath;

			if (m_remoteItemClass == ItemClass.Blob)
			{
				m_azureHelper = new AzureStorageHelper(connections, config);
			}
			else
			{
				m_remoteStorage = config.RemoteStorage;
			}
		}

		/// <summary>
		///     Fetches the files from Remote Storage.
		/// </summary>
		/// <param name="filesToFetch">The files to fetch.</param>
		/// <param name="sortOrder">The item sort order.</param>
		/// <returns></returns>
		public Dictionary<FileInfo, string> FetchItemsFromRemoteStorage(List<RemoteItemEntity> filesToFetch, ItemSortOrder sortOrder)
		{
			var fileList = filesToFetch.ToList();

			if (m_remoteItemClass == ItemClass.Blob)
			{
				var container = m_azureHelper.Container;

				// Build list of files for restore script
				var files = fileList.ToDictionary(
					file => new FileInfo($@"{m_localDirectory}\{file.Name.Replace(@"/", @"\")}"),
					file => file.BackupType.ToFriendlyString());

				// Sort by smallest file first
				var sortedFilesToFetch = sortOrder == ItemSortOrder.Size
					? fileList.OrderBy(x => x.Size)
					: fileList.OrderBy(x => x.Name);

				Console.WriteLine(
					$"Fetching files {(sortOrder == ItemSortOrder.Size ? "from smallest to largest" : "in alphabetical order")}] ...");

				foreach (var file in sortedFilesToFetch)
				{
					Console.Write($"Fetching [{file.Name}]: ");

					var item = container.GetBlobClient(file.Name);

					var localFilePath = $@"{m_localDirectory}\{file.Name.Replace(@"/", @"\")}";

					var fi = new FileInfo(localFilePath);

					// Skip the file if it exists
					if (fi.Exists && fi.Length == file.Size)
					{
						Common.SetFileCreationDate(file, fi);
						Console.WriteLine("Done.");
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

					item.DownloadTo(fi.FullName);

					Common.SetFileCreationDate(file, fi);
					Console.WriteLine("Done.");
				}

				return files;
			}
			else
			{
				// Build list of files for restore script
				var files = fileList.ToDictionary(
					file => new FileInfo($@"{file.PathOrUrl.Replace(m_remoteStorage, m_localDirectory)}\{file.Name}"),
					file => file.BackupType.ToFriendlyString());

				// Sort by smallest file first
				var sortedFilesToFetch = sortOrder == ItemSortOrder.Size
					? fileList.OrderBy(x => x.Size)
					: fileList.OrderBy(x => x.Name);

				Console.WriteLine($"Fetching files {(sortOrder == ItemSortOrder.Size ? "from smallest to largest" : "in alphabetical order")} ...");

				foreach (var file in sortedFilesToFetch)
				{
					Console.Write($"Fetching [{file.Name}]: ");

					var remoteFilePath = $@"{file.PathOrUrl}\{file.Name}";
					var localFilePath = remoteFilePath.Replace(m_remoteStorage, m_localDirectory);

					var localFile = new FileInfo(localFilePath);
					var remoteFile = new FileInfo(remoteFilePath);

					// Skip the file if it exists
					if (localFile.Exists && localFile.Length == file.Size)
					{
						Common.SetFileCreationDate(file, localFile);
						Console.WriteLine("Done.");
						continue;
					}

					if (localFile.DirectoryName != null)
					{
						var di = new DirectoryInfo(localFile.DirectoryName);
						if (!di.Exists)
						{
							di.Create();
						}
					}

					try
					{
						File.Copy(remoteFile.FullName, localFilePath);
						Common.SetFileCreationDate(file, localFile);
						Console.WriteLine("Done.");
					}
					catch (Exception)
					{
						Console.WriteLine($"Failed copying file from {remoteFile.FullName} to {localFilePath}.");
					}
				}

				return files;
			}
		}
	}
}
