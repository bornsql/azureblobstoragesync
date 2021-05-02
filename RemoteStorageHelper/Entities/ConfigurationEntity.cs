namespace RemoteStorageHelper.Entities
{
	public class ConfigurationEntity
	{
		public string Container { get; set; }
		public bool CopyFilesToRemoteStorage { get; set; }
		public bool DebugOverride { get; set; }
		public bool DumpFileList { get; set; }
		public bool FetchFilesFromRemoteStorage { get; set; }
		public string ItemSortOrder { get; set; }
		public string LocalPath { get; set; }
		public string EncryptedFileExtension { get; set; }
	}
}
