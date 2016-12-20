using System;

namespace RemoteStorageHelper
{
	public class RemoteItem
	{
		public string Name { get; set; }
		public string PathOrUrl { get; set; }
		public DateTimeOffset? LastModified { get; set; }
		public long Size { get; set; }
		public ItemType Type { get; set; }
		public DateTime? BackupDate { get; set; }
		public Backup BackupType { get; set; }
		public string FakePath { get; set; }
	}
}