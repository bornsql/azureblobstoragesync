using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;

namespace RemoteStorageHelper
{
	public class Common
	{
		private readonly DirectoryInfo m_localDirectory;
		private readonly string m_encryptedFileExtension;

		public NetworkCredential NetworkCredentials { private get; set; }

		public Common()
		{
			m_localDirectory = new DirectoryInfo(ConfigHelper.GetConfigurationValue("LocalPath"));
			m_encryptedFileExtension = ConfigHelper.GetConfigurationValue("EncryptedFileExtension");
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

			return ParseDate(s);
		}

		/// <summary>
		/// Parses the backup type from the file name.
		/// </summary>
		/// <param name="name">The file name.</param>
		/// <returns></returns>
		public static Backup ParseBackupTypeFromFileName(string name)
		{
			// We know nothing about this item name.
			// Hopefully it follows Ola Hallengren's naming convention, so let's start there

			// JUPITER$SQL2012_master_FULL_20140114_163121.bak
			var method = string.Empty;

			var splitter = name.Split('/'); // split out the file name from the RemoteStorage directory structure

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

			var files = m_localDirectory.GetFiles(encryptedFilesOnly ? $"*{m_encryptedFileExtension}" : "*.*",
				SearchOption.AllDirectories);

			foreach (var file in files)
			{
				localFiles.Add(file, file.FullName.Replace($@"{m_localDirectory.FullName}\", string.Empty));
			}

			return localFiles;
		}

		public static void SetFileCreationDate(RemoteItem file, FileSystemInfo fi)
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
			DateTime dt;
			return DateTime.TryParseExact(date, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)
				? (DateTime?)dt
				: null;
		}

		public static string GetStopAtTime()
		{
			return
				ParseDate(ConfigHelper.GetConfigurationValue("StopAtDateTime"))?.ToString("yyyy-MM-dd HH:mm:ss") ??
				DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
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
