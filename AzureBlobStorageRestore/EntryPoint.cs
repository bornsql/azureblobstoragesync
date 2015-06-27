using System;
using System.IO;
using System.Text;
using AzureBlobStorageHelper;

namespace AzureBlobStorageRestore
{
	public static class EntryPoint
	{
		static void Main(string[] args)
		{
			Console.WriteLine(ConsoleHelper.Header());
			var restoreHelper = new RestoreHelper();

			var database = ConfigHelper.GetConfigurationValue("DatabaseToRestore");
			var dumpFileList = ConfigHelper.GetConfigurationBoolean("DumpFileList");
			var restoreScriptFile = ConfigHelper.GetConfigurationValue("RestoreScriptFile");
			var restoreScriptMask = ConfigHelper.GetConfigurationValue("RestoreScriptMask");

			var dateTime = DateTime.Now.ToString(restoreScriptMask);
			var fileName = new FileInfo(restoreScriptFile.Replace("{%%DATABASE%%}", database).Replace("{%%MASK%%}", dateTime));
			StringBuilder outputScript;

			if (!dumpFileList)
			{
				outputScript = restoreHelper.GenerateRestoreScript(database);
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
				outputScript = restoreHelper.GenerateRestoreScript(database, true);

				// Write out the SQL Restore file
				using (var writer = new StreamWriter(ConfigHelper.GetConfigurationValue("DumpFile")))
				{
					writer.Write(outputScript.ToString());
				}
			}

		}
	}
}