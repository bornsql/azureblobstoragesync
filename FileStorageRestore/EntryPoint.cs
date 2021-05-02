using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using RemoteStorageHelper.Entities;
using RemoteStorageHelper.Enums;
using RemoteStorageHelper.Helpers;

namespace FileStorageRestore
{
	public static class EntryPoint
	{
		private static async Task Main(string[] args)
		{
			var config = JsonWrangler.ReadJsonItem<RestoreConfigurationEntity>(new FileInfo("restore.json"));

			var ver = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion;
			Console.Title = $"File Storage Restore Tool {ver}";
			Console.WriteLine(ConsoleHelper.Header(ver));
			var restoreHelper = new RestoreHelper(ItemClass.File);

			var database = config.DatabaseToRestore;
			var dumpFileList = config.DumpFileList;
			var restoreScriptFile = config.RestoreScriptFile;
			var restoreScriptMask = config.RestoreScriptMask;
			var sortOrder = config.ItemSortOrder.Equals("Size", StringComparison.InvariantCultureIgnoreCase)
				? ItemSortOrder.Size
				: ItemSortOrder.Name;

			var dateTime = DateTime.Now.ToString(restoreScriptMask);
			var fileName = new FileInfo(restoreScriptFile.Replace("{%%DATABASE%%}", database)
				.Replace("{%%MASK%%}", dateTime));
			StringBuilder outputScript;

			if (!dumpFileList)
			{
				outputScript = await restoreHelper.FetchFilesForRestoreScript(database, sortOrder);
				// Create output directory for the script filename if it doesn't exist
				if (fileName.Directory != null && !fileName.Directory.Exists)
				{
					fileName.Directory.Create();
				}

				// Write out the SQL Restore file
				await File.WriteAllTextAsync(fileName.FullName, outputScript.ToString());
			}
			else
			{
				outputScript = await restoreHelper.GenerateRestoreScript(database, true, sortOrder);

				// Write out the SQL Restore file
				await File.WriteAllTextAsync(config.DumpFile, outputScript.ToString());
			}
		}
	}
}
