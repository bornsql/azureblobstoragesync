using System;

namespace AzureBlobStorageHelper
{
	public class BlobItem
	{
		public string Name { get; set; }
		public string URL { get; set; }
		public DateTimeOffset? LastModified { get; set; }
		public long Size { get; set; }
		public BlobType Type { get; set; }
		public DateTime? BackupDate { get; set; }
		public Backup BackupType { get; set; }

		public enum BlobType
		{
			Block,
			Page,
			Directory
		}
	}
}