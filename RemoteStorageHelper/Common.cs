using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using RemoteStorageHelper.Entities;
using RemoteStorageHelper.Enums;
using RemoteStorageHelper.Helpers;

namespace RemoteStorageHelper
{
	public class Common
	{
		private readonly DirectoryInfo m_localDirectory;
		private readonly string m_encryptedFileExtension;

		public Common()
		{
			var config = JsonWrangler.ReadJsonItem<RestoreConfigurationEntity>(new FileInfo("restore.json"));

			m_localDirectory = new DirectoryInfo(config.LocalPath);
			m_encryptedFileExtension = config.EncryptedFileExtension;
		}

		/// <summary>
		/// Parses the name of the date from the file name.
		/// </summary>
		/// <param name="name">The file name.</param>
		/// <returns></returns>
		public static DateTime? ParseDateFromFileName(string name)
		{
			// We know nothing about this item name.
			// Hopefully it follows Ola Hallengren's naming convention, so let's start there

			// JUPITER$SQL2012_master_FULL_20140114_163121.bak

			var splitter = name.Split('/'); // split out the file name from the RemoteStorage directory structure

			var filename = splitter.Length == 1
				? splitter[0]
				: splitter[^1];

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

			return ParseDate(s);
		}

		/// <summary>
		/// Parses the backup type from the file name.
		/// </summary>
		/// <param name="name">The file name.</param>
		/// <returns></returns>
		public static BackupType ParseBackupTypeFromFileName(string name)
		{
			// We know nothing about this item name.
			// Hopefully it follows Ola Hallengren's naming convention, so let's start there

			// JUPITER$SQL2012_master_FULL_20140114_163121.bak
			var method = string.Empty;

			var splitter = name.Split('/'); // split out the file name from the RemoteStorage directory structure

			var filename = splitter.Length == 1
				? splitter[0]
				: splitter[^1];

			if (splitter.Length > 1) // hack out the backup type from the directory name
			{
				method = splitter[1].ToUpperInvariant();
			}
			else
			{
				// Could just return the types here, but no accounting for Unknown
				if (filename.Contains("_FULL_", StringComparison.CurrentCultureIgnoreCase) &&
					!filename.Contains("_FULL_COPY_ONLY_", StringComparison.CurrentCultureIgnoreCase))
				{
					method = "FULL";
				}
				else if (filename.Contains("_FULL_COPY_ONLY_", StringComparison.CurrentCultureIgnoreCase))
				{
					method = "FULL_COPY_ONLY";
				}
				else if (filename.Contains("_DIFF_", StringComparison.CurrentCultureIgnoreCase))
				{
					method = "DIFF";
				}
				else if (filename.Contains("_LOG_", StringComparison.CurrentCultureIgnoreCase))
				{
					method = "LOG";
				}
			}

			return method switch
			{
				"FULL" => BackupType.Full,
				"FULL_COPY_ONLY" => BackupType.CopyOnly,
				"DIFF" => BackupType.Differential,
				"LOG" => BackupType.TransactionLog,
				_ => BackupType.Unknown
			};
		}

		/// <summary>
		/// Gets the local files.
		/// </summary>
		/// <returns></returns>
		public Dictionary<FileInfo, string> GetLocalFiles(bool encryptedFilesOnly)
		{
			var localFiles = new Dictionary<FileInfo, string>();

			Console.WriteLine("Fetching list of files on local machine ...");
			if (!m_localDirectory.Exists)
			{
				return localFiles;
			}

			var files = m_localDirectory.GetFiles(encryptedFilesOnly
				? $"*{m_encryptedFileExtension}"
				: "*.*", SearchOption.AllDirectories);

			foreach (var file in files)
			{
				localFiles.Add(file, file.FullName.Replace($@"{m_localDirectory.FullName}\", string.Empty));
			}

			return localFiles;
		}

		public static void SetFileCreationDate(RemoteItemEntity file, FileSystemInfo fi)
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

		/// <summary>
		/// Parses the date from a string.
		/// </summary>
		/// <param name="date">The date as string.</param>
		/// <param name="format">The date format.</param>
		/// <returns></returns>
		public static DateTime? ParseDate(string date, string format = "yyyyMMdd_HHmmss")
		{
			return DateTime.TryParseExact(date, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)
				? (DateTime?) dt
				: null;
		}

		public IEnumerable<FileInfo> GetFileList(string fileSearchPattern, string rootFolderPath)
		{
			var pending = new Queue<string>();
			pending.Enqueue(rootFolderPath);
			string[] tmp;
			while (pending.Count > 0)
			{
				rootFolderPath = pending.Dequeue();
				tmp = Directory.GetFiles(rootFolderPath, fileSearchPattern);
				foreach (var t in tmp)
				{
					yield return new FileInfo(t);
				}

				tmp = Directory.GetDirectories(rootFolderPath);
				foreach (var t in tmp)
				{
					pending.Enqueue(t);
				}
			}
		}
	}
}
