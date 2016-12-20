﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace RemoteStorageHelper
{
	public class RemoteFetchHelper
	{
		private readonly ItemClass m_remoteItemClass;

		private readonly AzureStorageHelper m_azureHelper;
		private readonly string m_localDirectory;
		private readonly string m_remoteStorage;

		public RemoteFetchHelper(ItemClass itemClass)
		{
			m_remoteItemClass = itemClass;

			m_localDirectory = ConfigHelper.GetConfigurationValue("LocalPath");

			if (m_remoteItemClass == ItemClass.Blob)
			{
				m_azureHelper = new AzureStorageHelper();
			}
			else
			{
				m_remoteStorage = ConfigHelper.GetConfigurationValue("RemoteStorage");
			}
		}

		/// <summary>
		/// Fetches the files from Remote Storage.
		/// </summary>
		/// <param name="filesToFetch">The files to fetch.</param>
		/// <param name="sortOrder">The item sort order.</param>
		/// <returns></returns>
		public Dictionary<FileInfo, string> FetchItemsFromRemoteStorage(IEnumerable<RemoteItem> filesToFetch,
			ItemSortOrder sortOrder)
		{
			var fileList = filesToFetch.ToList();

			if (m_remoteItemClass == ItemClass.Blob)
			{
				var blobClient = m_azureHelper.StorageAccount.CreateCloudBlobClient();
				var container = blobClient.GetContainerReference(m_azureHelper.ContainerName);
				const int segmentSize = 1 * 1024 * 1024; // 1 MB chunk
														 // int segmentSize = 64 * 1024; // 64 KB chunk

				// Build list of files for restore script
				var files = fileList.ToDictionary(file => new FileInfo($@"{m_localDirectory}\{file.Name.Replace(@"/", @"\")}"),
					file => file.BackupType.ToFriendlyString());

				// Sort by smallest file first
				var sortedFilesToFetch = sortOrder == ItemSortOrder.Size
					? fileList.OrderBy(x => x.Size)
					: fileList.OrderBy(x => x.Name);

				Console.WriteLine("Fetching files {0} with block size [{1}] ...",
					sortOrder == ItemSortOrder.Size ? "from smallest to largest" : "in alphabetical order",
					segmentSize.ToString("N0", CultureInfo.InvariantCulture));

				foreach (var file in sortedFilesToFetch)
				{
					Console.Write("Fetching [{0}]: ", file.Name);

					// Retrieve reference to a blob named "myblob".
					var blockBlob = container.GetBlockBlobReference(file.Name);

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

					blockBlob.FetchAttributes();
					var blobLengthRemaining = blockBlob.Properties.Length;
					long startPosition = 0;

					using (var progress = new ProgressBar())
					{
						do
						{
							progress.Report((double)startPosition / blockBlob.Properties.Length);
							Thread.Sleep(1);
							var blockSize = Math.Min(segmentSize, blobLengthRemaining);
							var blobContents = new byte[blockSize];
							using (var ms = new MemoryStream())
							{
								blockBlob.DownloadRangeToStream(ms, startPosition, blockSize);
								ms.Position = 0;
								ms.Read(blobContents, 0, blobContents.Length);

								using (var fileStream = new FileStream(@fi.FullName, FileMode.OpenOrCreate))
								{
									fileStream.Position = startPosition;
									fileStream.Write(blobContents, 0, blobContents.Length);
								}
							}
							startPosition += blockSize;
							blobLengthRemaining -= blockSize;
						} while (blobLengthRemaining > 0);
					}

					Common.SetFileCreationDate(file, fi);
					Console.WriteLine("Done.");
				}

				return files;
			}
			else
			{
				// Build list of files for restore script
				var files = fileList.ToDictionary(file => new FileInfo($@"{file.PathOrUrl.Replace(m_remoteStorage, m_localDirectory)}\{file.Name}"),
					file => file.BackupType.ToFriendlyString());

				// Sort by smallest file first
				var sortedFilesToFetch = sortOrder == ItemSortOrder.Size
					? fileList.OrderBy(x => x.Size)
					: fileList.OrderBy(x => x.Name);

				Console.WriteLine("Fetching files {0} ...",
					sortOrder == ItemSortOrder.Size ? "from smallest to largest" : "in alphabetical order");

				foreach (var file in sortedFilesToFetch)
				{
					Console.Write("Fetching [{0}]: ", file.Name);

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
						Console.WriteLine("Failed copying file from {0} to {1}.", remoteFile.FullName, localFilePath);
					}
				}

				return files;
			}
		}
	}
}
