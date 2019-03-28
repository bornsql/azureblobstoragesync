using System;
using System.Collections.Generic;
using System.Net;

namespace RemoteStorageHelper
{
	public class FileStorageHelper
	{
		private readonly Common m_common;
		private readonly string m_remoteStorage;
		private readonly string m_remoteUsername;
		private readonly string m_remotePassword;

		public FileStorageHelper()
		{
			m_common = new Common();
			m_remoteStorage = ConfigHelper.GetConfigurationValue("RemoteStorage");
			m_remoteUsername = ConfigHelper.GetFileStorageUsername();
			m_remotePassword = ConfigHelper.GetFileStoragePassword();
		}

		/// <summary>
		/// Parses the File Storage account for a list of files to restore.
		/// </summary>
		/// <returns></returns>
		public List<RemoteItem> GetFileItems()
		{
			Console.WriteLine("Fetching list of items in File Storage ...");

			var fileItems = new List<RemoteItem>();

			using (new NetworkConnection(m_remoteStorage, new NetworkCredential(m_remoteUsername, m_remotePassword)))
			{
				// Loop over items within the directory and fetch the files
				foreach (var item in m_common.GetFileList("*.*", m_remoteStorage))
				{
					if (item.Name.Length >= 15)
					{
						var fi = new RemoteItem
						{
							Name = item.Name,
							BackupDate = Common.ParseDateFromFileName(item.Name),
							BackupType = Common.ParseBackupTypeFromFileName(item.Name),
							PathOrUrl = item.DirectoryName,
							FakePath = item.FullName.Replace(m_remoteStorage, "").Replace("\\", "//"),
							Size = item.Length,
							LastModified = item.LastWriteTimeUtc,
							Type = ItemType.File
						};


						if (fi.FakePath.StartsWith("//"))
						{
							fi.FakePath = fi.FakePath.Substring(2, fi.FakePath.Length - 2);
						}
						fileItems.Add(fi);
					}

					Console.Write($"{Environment.NewLine}Retrieved {fileItems.Count} item(s) ...");
				}
				Console.WriteLine();
			}
			return fileItems;
		}
	}
}
