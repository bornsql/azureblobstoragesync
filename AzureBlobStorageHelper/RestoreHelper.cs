using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace AzureBlobStorageHelper
{
	public class RestoreHelper
	{
		private readonly AzureHelper m_azureHelper;
		private readonly string m_localDirectory;

		public RestoreHelper()
		{
			m_azureHelper = new AzureHelper();

			m_localDirectory = ConfigHelper.GetConfigurationValue("LocalPath");
		}

		/// <summary>
		/// Downloads the files from Azure.
		/// </summary>
		/// <param name="filesToDownload">The files to download.</param>
		/// <returns></returns>
		private Dictionary<FileInfo, string> DownloadFilesFromAzure(IEnumerable<BlobItem> filesToDownload)
		{
			var files = new Dictionary<FileInfo, string>();
			var blobClient = m_azureHelper.StorageAccount.CreateCloudBlobClient();
			var container = blobClient.GetContainerReference(m_azureHelper.ContainerName);

			foreach (var file in filesToDownload)
			{
				// Retrieve reference to a blob named "myblob".
				var blockBlob = container.GetBlockBlobReference(file.Name);

				var localFilePath = string.Format(@"{0}\{1}", m_localDirectory, file.Name.Replace(@"/", @"\"));

				var fi = new FileInfo(localFilePath);
				files.Add(fi, file.BackupType.ToFriendlyString());

				// Skip the file if it exists
				if (fi.Exists)
				{
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
			}

			return files;
		}

		/// <summary>
		/// Parses a list of blob items to obtain the latest backups
		/// </summary>
		/// <param name="files">The files as a list of blob items.</param>
		/// <param name="database">The database.</param>
		/// <returns></returns>
		private IEnumerable<BlobItem> ParseLatestBackup(List<BlobItem> files, string database)
		{
			var filesToDownload = new List<BlobItem>();

			var possibleFiles = files.FindAll(x => x.Name.StartsWith(database)).OrderBy(y => y.BackupDate).ToList();

			// Find the latest full backup
			var latestFullBackup = possibleFiles.FindLast(x => x.BackupType == Backup.Full);

			if (latestFullBackup == null)
			{
				return filesToDownload;
			}
			filesToDownload.Add(latestFullBackup);

			// Look for latest differential after full backup
			var latestDifferentialBackup =
				possibleFiles.FindLast(x => x.BackupType == Backup.Differential && x.BackupDate > latestFullBackup.BackupDate);

			if (latestDifferentialBackup != null)
			{
				filesToDownload.Add(latestDifferentialBackup);
			}

			// Look for all logs after the full backup or differential backup
			var latestLogBackups =
				possibleFiles.FindAll(
					x =>
						x.BackupType == Backup.TransactionLog &&
						(x.BackupDate >
						 (latestDifferentialBackup != null ? latestDifferentialBackup.BackupDate : latestFullBackup.BackupDate)));

			if (latestLogBackups.Count > 0)
			{
				filesToDownload.AddRange(latestLogBackups);
			}

			return filesToDownload;
		}

		/// <summary>
		/// Parses the date from a string.
		/// </summary>
		/// <param name="date">The date as string.</param>
		/// <param name="format">The date format.</param>
		/// <returns></returns>
		internal static DateTime? ParseDate(string date, string format = "yyyyMMdd_HHmmss")
		{
			DateTime dt;
			return DateTime.TryParseExact(date, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out dt)
				? (DateTime?)dt
				: null;
		}

		/// <summary>
		/// Builds the restore script.
		/// </summary>
		/// <param name="files">The downloaded files.</param>
		/// <returns></returns>
		private StringBuilder BuildRestoreScript(Dictionary<FileInfo, string> files)
		{
			var database = ConfigHelper.GetConfigurationValue("DatabaseRestoredName");
			var localDatabasePath = new DirectoryInfo(ConfigHelper.GetConfigurationValue("DatabaseRestoredPath"));
			var databaseFilePrefix = ConfigHelper.GetConfigurationValue("DatabaseRestoredFilePrefix");

			var template = new StringBuilder();

			var script = new StringBuilder();
			var nl = Environment.NewLine;

			script.AppendFormat("-- AzureBlobStorageRestore Script to restore {2}. Generated on {0}{1}{1}",
				DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), nl, database);
			const string batchSeparator = @"GO";

			script.AppendLine(@"USE [master];");
			script.AppendLine(batchSeparator);

			script.AppendFormat(@"IF DB_ID('[{0}]') IS NOT NULL ALTER DATABASE [{0}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;{1}", database, nl);
			script.AppendLine(batchSeparator);

			var full = true;

			foreach (var fileInfo in files)
			{
				if (full)
				{
					// We want to run the template for the first file only (full backup)
					var line = string.Format(@"DECLARE @fullBackup NVARCHAR(MAX) = N'{0}';", fileInfo.Key.FullName);
					template.AppendLine(line);
					line = string.Format(@"DECLARE @path NVARCHAR(255) = N'{0}\{1}';", localDatabasePath.FullName, databaseFilePrefix);
					template.AppendLine(line);

					// Create template restore script for moving files
					line = string.Format(
						@"DECLARE @template NVARCHAR(MAX) = N'RESTORE DATABASE [{0}] FROM DISK = N''{1}'' WITH {{%%MOVE%%}} REPLACE, NOUNLOAD, NORECOVERY, STATS = 5;'",
						database, fileInfo.Key.FullName);
					template.AppendLine(line);

					// Create the table variable to populate the backup file list
					line =
						@"DECLARE @FileListInfo TABLE ([FileListInfoID] INT IDENTITY(1,1), [LogicalName] NVARCHAR(128), [PhysicalName] NVARCHAR(260), [Type] CHAR(1), [FileGroupName] NVARCHAR(128), [Size] NUMERIC(20, 0), [MaxSize] NUMERIC(20, 0), [FileId] INT, [CreateLSN] NUMERIC(25, 0), [DropLSN] NUMERIC(25, 0), [UniqueId] UNIQUEIDENTIFIER, [ReadOnlyLSN] NUMERIC(25, 0), [ReadWriteLSN] NUMERIC(25, 0), [BackupSizeInBytes] BIGINT, [SourceBlockSize] INT, [FilegroupId] INT, [LogGroupGUID] UNIQUEIDENTIFIER, [DifferentialBaseLSN] NUMERIC(25), [DifferentialBaseGUID] UNIQUEIDENTIFIER, [IsReadOnly] INT, [IsPresent] INT, [TDEThumbprint] NVARCHAR(128));";
					template.AppendLine(line);

					// Declare the variables to loop through the backup file list
					line = @"DECLARE @sql NVARCHAR(MAX) = N'', @i INT, @x INT = 1;";
					template.AppendLine(line);

					line = string.Format(@"DECLARE @header NVARCHAR(MAX) = 'RESTORE FILELISTONLY FROM DISK = ''{0}''';",
						fileInfo.Key.FullName);
					template.AppendLine(line);

					// Get the file header info for this backup
					line = @"INSERT INTO @FileListInfo EXEC (@header);";
					template.AppendLine(line);


					line = @"SELECT @i = MAX([FileListInfoID]) FROM @FileListInfo;";
					template.AppendLine(line);

					line =
						@"WHILE @x <= @i BEGIN SELECT @sql = @sql + N' MOVE N''' + [LogicalName] + N''' TO N''' + @path + [LogicalName] + CASE WHEN [Type] = N'D' AND [FileId] = 1 THEN N'.mdf' WHEN [Type] = N'D' AND [FileId] <> 1 THEN N'.ndf' ELSE N'.ldf' END + ''',' FROM @FileListInfo WHERE [FileListInfoID] = @x; SELECT @x = @x + 1; END;";
					template.AppendLine(line);

					line = @"SELECT @sql = REPLACE(@template, N'{%%MOVE%%}', @sql);";
					template.AppendLine(line);

					line = @"EXEC sp_executesql @sql;";
					template.AppendLine(line);

					script.AppendLine(template.ToString());
					script.AppendLine(batchSeparator);

					full = false; // Now run the rest of the script
				}
				else
				{
					script.AppendFormat(
						@"RESTORE {2} [{0}] FROM DISK = N'{1}' WITH FILE = 1, NOUNLOAD, REPLACE, NORECOVERY, STATS = 5;{3}", database,
						fileInfo.Key.FullName, fileInfo.Value, nl);
					script.AppendLine(batchSeparator);
				}
			}

			script.AppendFormat(@"RESTORE DATABASE [{0}] WITH RECOVERY;{1}", database, nl);
			script.AppendLine(batchSeparator);

			script.AppendFormat(@"ALTER DATABASE [{0}] SET MULTI_USER;{1}", database, nl);
			script.AppendLine(batchSeparator);

			script.AppendFormat(@"DBCC CHECKDB ([{0}]) WITH ALL_ERRORMSGS, NO_INFOMSGS, DATA_PURITY;{1}", database, nl);
			script.AppendLine(batchSeparator);

			return script;
		}

		/// <summary>
		/// Generates the restore script.
		/// </summary>
		/// <param name="database">The database.</param>
		/// <returns></returns>
		public StringBuilder GenerateRestoreScript(string database)
		{
			// var localFiles = GetLocalFiles();

			var blobItems = m_azureHelper.GetBlobItems();

			Console.WriteLine("Number of items found in Azure Container: {0}", blobItems.Count);
			Console.WriteLine();

			Console.WriteLine("Starting file review ...");

			var filesToDownload = ParseLatestBackup(blobItems, database);

			// Actually download the files
			var files = DownloadFilesFromAzure(filesToDownload);

			// Generate the script and return it
			return BuildRestoreScript(files);
		}

		/// <summary>
		/// Generates the restore script.
		/// </summary>
		/// <param name="database">The database.</param>
		/// <param name="dumpFileList">Dump list of all files on Azure</param>
		/// <returns></returns>
		public StringBuilder GenerateRestoreScript(string database, bool dumpFileList)
		{
			if (!dumpFileList)
			{
				return GenerateRestoreScript(database);
			}

			var blobItems = m_azureHelper.GetBlobItems();

			Console.WriteLine("Number of items found in Azure Container: {0}", blobItems.Count);
			Console.WriteLine();

			Console.WriteLine("Dumping list of Blob Items to disk ...");

			var fileList = new StringBuilder();

			foreach (var blobItem in blobItems)
			{
				fileList.AppendLine(blobItem.Name);
			}
			return fileList;
		}
	}
}
