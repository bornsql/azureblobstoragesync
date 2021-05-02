using System;
using RemoteStorageHelper.Enums;

namespace RemoteStorageHelper.Entities
{
	public class RemoteItemEntity
	{
		public string Name { get; set; }
		public string PathOrUrl { get; set; }
		public DateTimeOffset? LastModified { get; set; }
		public long Size { get; set; }
		public ItemType Type { get; set; }
		public DateTime? BackupDate { get; set; }
		public BackupType BackupType { get; set; }
		public string FakePath { get; set; }
	}
}
