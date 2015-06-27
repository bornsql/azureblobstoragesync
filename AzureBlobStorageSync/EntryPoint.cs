using System;
using AzureBlobStorageHelper;

namespace AzureBlobStorageSync
{
	public static class EntryPoint
	{
		private static bool m_isDebug;

		static void Main(string[] args)
		{
#if DEBUG
			m_isDebug = true;
#else
			m_isDebug = false;
#endif

			Console.WriteLine(ConsoleHelper.Header());
			var syncHelper = new SyncHelper(m_isDebug);
			syncHelper.Sync();
		}
	}
}