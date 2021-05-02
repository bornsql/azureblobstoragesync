using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RemoteStorageHelper.Entities;
using RemoteStorageHelper.Enums;

namespace RemoteStorageHelper.Helpers
{
	public class RestoreHelper
	{
		private readonly AzureStorageHelper m_azureHelper;
		private readonly FileStorageHelper m_fileHelper;

		private readonly ItemClass m_remoteItemClass;

		private readonly RestoreConfigurationEntity m_config;

		public RestoreHelper(ItemClass itemClass)
		{
			m_remoteItemClass = itemClass;
			m_config = JsonWrangler.ReadJsonItem<RestoreConfigurationEntity>(new FileInfo("restore.json"));
			var connections = JsonWrangler.ReadJsonItem<ConnectionEntity>(new FileInfo("connections.json"));

			if (m_remoteItemClass == ItemClass.Blob)
			{
				m_azureHelper = new AzureStorageHelper(connections, m_config);
			}
			else
			{
				m_fileHelper = new FileStorageHelper(m_config, connections);
			}
		}

		/// <summary>
		/// Parses a list of blob items to obtain the latest backups
		/// </summary>
		/// <param name="files">The files as a list of blob items.</param>
		/// <param name="database">The database.</param>
		/// <returns></returns>
		private List<RemoteItemEntity> ParseLatestBackup(List<RemoteItemEntity> files, string database)
		{
			var filesToFetch = new List<RemoteItemEntity>();

			var possibleFiles = m_remoteItemClass == ItemClass.Blob
				? files.FindAll(x => x.Name.StartsWith(database)).OrderBy(y => y.BackupDate).ToList()
				: files.FindAll(x => x.FakePath.StartsWith(database)).OrderBy(y => Common.ParseDate(y.FakePath)).ToList();

			if (m_config.StopAtEnabled)
			{
				var stopAtDateTime = m_config.StopAtDateTime;
				var flag = -1;
				for (var i = 0; i < possibleFiles.Count; i++)
				{
					if (Common.ParseDateFromFileName(possibleFiles[i].Name) > stopAtDateTime)
					{
						flag = i + 1;
						break;
					}
				}

				if (flag > -1)
				{
					possibleFiles.RemoveRange(flag, possibleFiles.Count - flag);
				}
			}

			// Find the latest full backup
			var latestFullBackup = possibleFiles.FindLast(x => x.BackupType == BackupType.Full);

			if (latestFullBackup == null)
			{
				return filesToFetch;
			}

			filesToFetch.Add(latestFullBackup);

			// Look for latest differential after full backup
			var latestDifferentialBackup = possibleFiles.FindLast(x =>
				x.BackupType == BackupType.Differential && x.BackupDate > latestFullBackup.BackupDate);

			if (latestDifferentialBackup != null)
			{
				filesToFetch.Add(latestDifferentialBackup);
			}

			// Look for all logs after the full backup or differential backup
			var latestLogBackups = possibleFiles.FindAll(x => x.BackupType == BackupType.TransactionLog &&
															  (x.BackupDate > (latestDifferentialBackup != null
																  ? latestDifferentialBackup.BackupDate
																  : latestFullBackup.BackupDate)));

			if (latestLogBackups.Count > 0)
			{
				filesToFetch.AddRange(latestLogBackups);
			}

			return filesToFetch.ToList();
		}

		/// <summary>
		/// Builds the restore script.
		/// </summary>
		/// <param name="files">The fetched files.</param>
		/// <returns></returns>
		private StringBuilder BuildRestoreScript(Dictionary<FileInfo, string> files)
		{
			var database = m_config.DatabaseRestoredName;
			var localDatabasePath = new DirectoryInfo(m_config.DatabaseRestoredPath);
			var databaseFilePrefix = m_config.DatabaseRestoredFilePrefix;
			var stopAtEnabled = m_config.StopAtEnabled;
			var stopAtDateTime = m_config.StopAtDateTime;

			var template = new StringBuilder();

			var script = new StringBuilder();
			var nl = Environment.NewLine;

			script.Append($"-- RemoteStorageRestore Script to restore {database}. Generated on {DateTime.Now:yyyy-MM-dd HH:mm:ss}{nl}{nl}");
			const string batchSeparator = @"GO";

			script.AppendLine(@"USE [master];");
			script.AppendLine(batchSeparator);

			script.Append($@"IF DB_ID('[{database}]') IS NOT NULL ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;{nl}");
			script.AppendLine(batchSeparator);

			var full = true;

			foreach (var fileInfo in files)
			{
				if (full)
				{
					var line = @"SET NOCOUNT ON;";
					template.AppendLine(line);

					// We want to run the template for the first file only (full backup)
					line = $@"DECLARE @fullBackup NVARCHAR(MAX) = N'{fileInfo.Key.FullName}';";
					template.AppendLine(line);
					line = $@"DECLARE @path NVARCHAR(255) = N'{localDatabasePath.FullName}\{databaseFilePrefix}';";
					template.AppendLine(line);

					// Create template restore script for moving files
					line =
						$@"DECLARE @template NVARCHAR(MAX) = N'RESTORE DATABASE [{database}] FROM DISK = N''{fileInfo.Key.FullName
						}'' WITH {{%%MOVE%%}} REPLACE, NOUNLOAD, NORECOVERY, STATS = 5;'";
					template.AppendLine(line);

					// Create the temp table to populate the backup file list
					line = @"CREATE TABLE #FileListInfo ([FileListInfoID] INT IDENTITY(1,1), [LogicalName] NVARCHAR(128), [PhysicalName] NVARCHAR(260), [Type] CHAR(1), [FileGroupName] NVARCHAR(128), [Size] NUMERIC(20, 0), [MaxSize] NUMERIC(20, 0), [FileId] INT, [CreateLSN] NUMERIC(25, 0), [DropLSN] NUMERIC(25, 0), [UniqueId] UNIQUEIDENTIFIER, [ReadOnlyLSN] NUMERIC(25, 0), [ReadWriteLSN] NUMERIC(25, 0), [BackupSizeInBytes] BIGINT, [SourceBlockSize] INT, [FilegroupId] INT, [LogGroupGUID] UNIQUEIDENTIFIER, [DifferentialBaseLSN] NUMERIC(25), [DifferentialBaseGUID] UNIQUEIDENTIFIER, [IsReadOnly] INT, [IsPresent] INT, [TDEThumbprint] NVARCHAR(128));";
					template.AppendLine(line);

					line = $@"DECLARE @header NVARCHAR(MAX) = 'RESTORE FILELISTONLY FROM DISK = ''{fileInfo.Key.FullName}''';";
					template.AppendLine(line);

					// Get the file header info for this backup
					// If restoring to SQL Server 2016, there is an extra column at the end
					line = @"BEGIN TRY INSERT INTO #FileListInfo EXEC (@header); END TRY BEGIN CATCH ALTER TABLE #FileListInfo ADD [SnapshotUrl] NVARCHAR(128); INSERT INTO #FileListInfo EXEC (@header); END CATCH";
					template.AppendLine(line);

					// Declare the variables to loop through the backup file list
					line = @"DECLARE @sql NVARCHAR(MAX) = N'', @i INT, @x INT = 1;";
					template.AppendLine(line);

					line = @"SELECT @i = MAX([FileListInfoID]) FROM #FileListInfo;";
					template.AppendLine(line);

					line = @"WHILE @x <= @i BEGIN SELECT @sql = @sql + N' MOVE N''' + [LogicalName] + N''' TO N''' + @path + [LogicalName] + CASE WHEN [Type] = N'D' AND [FileId] = 1 THEN N'.mdf' WHEN [Type] = N'D' AND [FileId] <> 1 THEN N'.ndf' ELSE N'.ldf' END + ''',' FROM #FileListInfo WHERE [FileListInfoID] = @x; SELECT @x = @x + 1; END;";
					template.AppendLine(line);

					line = @"SELECT @sql = REPLACE(@template, N'{%%MOVE%%}', @sql);";
					template.AppendLine(line);

					// Drop the temp table
					line = @"DROP TABLE[#FileListInfo];";
					template.AppendLine(line);

					line = @"SET NOCOUNT OFF;";
					template.AppendLine(line);

					// Generate the T-SQL
					line = @"EXEC sp_executesql @sql;";
					template.AppendLine(line);

					script.AppendLine(template.ToString());
					script.AppendLine(batchSeparator);

					full = false; // Now run the rest of the script
				}
				else
				{
					script.Append($@"RESTORE {fileInfo.Value} [{database}] FROM DISK = N'{fileInfo.Key.FullName}' WITH FILE = 1, NOUNLOAD, REPLACE, NORECOVERY, {(stopAtEnabled
							? $"STOPAT = '{stopAtDateTime:yyyy-MM-dd HH:mm:ss}', "
							: string.Empty)}STATS = 10;{nl}");
					script.AppendLine(batchSeparator);
				}
			}

			script.Append($@"RESTORE DATABASE [{database}] WITH RECOVERY;{nl}");
			script.AppendLine(batchSeparator);

			script.Append($@"DBCC CHECKDB ([{database}]) WITH ALL_ERRORMSGS, NO_INFOMSGS, DATA_PURITY;{nl}");
			script.AppendLine(batchSeparator);

			script.Append($@"ALTER DATABASE [{database}] SET MULTI_USER;{nl}");
			script.AppendLine(batchSeparator);

			return script;
		}

		/// <summary>
		/// Fetches the files from Remote Storage for the restore script.
		/// </summary>
		/// <param name="database">The database.</param>
		/// <param name="sortOrder">The sort order.</param>
		/// <returns></returns>
		public async Task<StringBuilder> FetchFilesForRestoreScript(string database, ItemSortOrder sortOrder)
		{
			var remoteItems = m_remoteItemClass == ItemClass.Blob
				? await m_azureHelper.GetBlobItemListAsync()
				: m_fileHelper.GetFileItems();

			Console.WriteLine($"Number of items found in Remote Storage: {remoteItems.Count}");
			Console.WriteLine();

			Console.WriteLine("Starting file review ...");

			var filesToFetch = ParseLatestBackup(remoteItems, database);

			var rfh = new RemoteFetchHelper(m_remoteItemClass);

			// Actually fetch the files
			var files = rfh.FetchItemsFromRemoteStorage(filesToFetch, sortOrder);

			// Generate the script and return it
			return BuildRestoreScript(files);
		}

		/// <summary>
		/// Generates the restore script.
		/// </summary>
		/// <param name="database">The database.</param>
		/// <param name="dumpFileList">Dump list of all files on RemoteStorage</param>
		/// <param name="sortOrder">The sort order</param>
		/// <returns></returns>
		public async Task<StringBuilder> GenerateRestoreScript(string database, bool dumpFileList, ItemSortOrder sortOrder)
		{
			if (!dumpFileList)
			{
				return await FetchFilesForRestoreScript(database, sortOrder);
			}

			var remoteItems = m_remoteItemClass == ItemClass.Blob
				? await m_azureHelper.GetBlobItemListAsync()
				: m_fileHelper.GetFileItems();

			Console.WriteLine($"Number of items found in Remote Storage: {remoteItems.Count}");
			Console.WriteLine();

			Console.WriteLine("Dumping list of Blob Items to disk ...");

			var fileList = new StringBuilder();

			foreach (var item in remoteItems)
			{
				fileList.AppendLine(item.Name);
			}

			return fileList;
		}
	}
}
