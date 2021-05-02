using System;
using System.Diagnostics;
using System.Reflection;
using RemoteStorageHelper.Enums;
using RemoteStorageHelper.Helpers;

namespace FileStorageSync
{
	public static class EntryPoint
	{
		private static bool m_isDebug;

		private static void Main(string[] args)
		{
#if DEBUG
			m_isDebug = true;
#endif

			var ver = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion;
			Console.Title = $"File Storage Sync Tool {ver}";
			Console.WriteLine(ConsoleHelper.Header(ver));
			var syncHelper = new SyncHelper(ItemClass.File, m_isDebug);
			syncHelper.SyncFileStorage();
		}
	}
}
