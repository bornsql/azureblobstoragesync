using RemoteStorageHelper.Enums;

namespace RemoteStorageHelper
{
	public static class BackupLevelExtensions
	{
		/// <summary>
		/// Extension method to return a friendly string for this enumerator.
		/// </summary>
		/// <param name="me">The Backup Enumerator</param>
		/// <returns></returns>
		public static string ToFriendlyString(this BackupType me)
		{
			return me switch
			{
				BackupType.Full => "DATABASE",
				BackupType.CopyOnly => "DATABASE",
				BackupType.Differential => "DATABASE",
				BackupType.TransactionLog => "LOG",
				_ => "--"
			};
		}
	}
}
