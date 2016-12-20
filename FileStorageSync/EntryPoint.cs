using System;
using System.Diagnostics;
using System.Reflection;
using RemoteStorageHelper;

namespace FileStorageSync
{
	public static class EntryPoint
	{
		private static bool m_isDebug;

		static void Main(string[] args)
		{
			Console.Title = $"File Storage Sync Tool {FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion}";
			Console.WriteLine(ConsoleHelper.Header());
			var syncHelper = new SyncHelper(ItemClass.File, m_isDebug);
			syncHelper.SyncFileStorage();
		}
	}
}