﻿using System;
using System.Diagnostics;
using System.Reflection;
using RemoteStorageHelper;

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

			Console.Title = $"Azure Blob Storage Sync Tool {FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion}";
			Console.WriteLine(ConsoleHelper.Header());
			var syncHelper = new SyncHelper(ItemClass.Blob, m_isDebug);
			syncHelper.SyncAzureStorage();
		}
	}
}