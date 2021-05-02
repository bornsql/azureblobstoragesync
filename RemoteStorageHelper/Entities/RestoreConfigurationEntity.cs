using System;

namespace RemoteStorageHelper.Entities
{
	public class RestoreConfigurationEntity : ConfigurationEntity
	{
		public string DatabaseRestoredName { get; set; }
		public string DatabaseRestoredFilePrefix { get; set; }
		public string DatabaseRestoredPath { get; set; }
		public string DatabaseToRestore { get; set; }
		public string DumpFile { get; set; }
		public string RemoteStorage { get; set; }
		public string RestoreScriptFile { get; set; }
		public string RestoreScriptMask { get; set; }
		public DateTime StopAtDateTime { get; set; }
		public bool StopAtEnabled { get; set; }
	}
}
