namespace AzureBlobStorageHelper
{
	public enum Backup
	{
		Unknown,
		Full,
		CopyOnly,
		Differential,
		TransactionLog
	}

	public static class BackupLevelExtensions
	{
		/// <summary>
		/// Extension method to return a friendly string for this enumerator.
		/// </summary>
		/// <param name="me">The Backup Enumerator</param>
		/// <returns></returns>
		public static string ToFriendlyString(this Backup me)
		{
			switch (me)
			{
				case Backup.Full:
				case Backup.CopyOnly:
				case Backup.Differential:
					return "DATABASE";
				case Backup.TransactionLog:
					return "LOG";
				default:
					return "--";
			}
		}
	}
}