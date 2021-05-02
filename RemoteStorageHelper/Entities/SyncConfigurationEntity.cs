namespace RemoteStorageHelper.Entities
{
	public class SyncConfigurationEntity : ConfigurationEntity
	{
		public bool CompressAndEncrypt { get; set; }
		public bool EncryptOnly { get; set; }
		public bool FetchExplicitFilesFromRemoteStorage { get; set; }
		public string ExplicitFilesToFetchMatchingString { get; set; }
		public bool DeleteMissingFilesFromRemoteStorage { get; set; }
		public bool DeleteMissingFilesFromLocalStorage { get; set; }
		public bool DeleteExplicitFilesFromRemoteStorage { get; set; }
		public string ExplicitFilesToDeleteMatchingString { get; set; }
	}
}
