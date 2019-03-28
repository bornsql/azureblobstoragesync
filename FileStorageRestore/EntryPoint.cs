using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using RemoteStorageHelper;

namespace FileStorageRestore
{
	public static class EntryPoint
	{
		static void Main(string[] args)
		{
            var ver = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion;
            Console.Title = $"File Storage Restore Tool {ver}";
			Console.WriteLine(ConsoleHelper.Header(ver));
			var restoreHelper = new RestoreHelper(ItemClass.File);

			var database = ConfigHelper.GetConfigurationValue("DatabaseToRestore");
			var dumpFileList = ConfigHelper.GetConfigurationBoolean("DumpFileList");
			var restoreScriptFile = ConfigHelper.GetConfigurationValue("RestoreScriptFile");
			var restoreScriptMask = ConfigHelper.GetConfigurationValue("RestoreScriptMask");
			var sortOrder = ConfigHelper.GetConfigurationValue("ItemSortOrder")
				.Equals("Size", StringComparison.InvariantCultureIgnoreCase)
				? ItemSortOrder.Size
				: ItemSortOrder.Name;

			var dateTime = DateTime.Now.ToString(restoreScriptMask);
			var fileName = new FileInfo(restoreScriptFile.Replace("{%%DATABASE%%}", database).Replace("{%%MASK%%}", dateTime));
			StringBuilder outputScript;

			if (!dumpFileList)
			{
				outputScript = restoreHelper.FetchFilesForRestoreScript(database, sortOrder);
				// Create output directory for the script filename if it doesn't exist
				if (fileName.Directory != null && !fileName.Directory.Exists)
				{
					fileName.Directory.Create();
				}

				// Write out the SQL Restore file
				using (var writer = new StreamWriter(fileName.FullName))
				{
					writer.Write(outputScript.ToString());
				}
			}
			else
			{
				outputScript = restoreHelper.GenerateRestoreScript(database, true, sortOrder);

				// Write out the SQL Restore file
				using (var writer = new StreamWriter(ConfigHelper.GetConfigurationValue("DumpFile")))
				{
					writer.Write(outputScript.ToString());
				}
			}

		}
	}
}