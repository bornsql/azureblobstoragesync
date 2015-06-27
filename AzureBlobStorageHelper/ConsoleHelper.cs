using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace AzureBlobStorageHelper
{
	public static class ConsoleHelper
	{
		public static string Header()
		{
			var header = new StringBuilder();
			var s = string.Format("{0} version {1}", Process.GetCurrentProcess().ProcessName, FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion);
			header.AppendLine(s);
			header.AppendLine("Copyright (c) 2015 Randolph West. All rights reserved.");
			return header.ToString();
		}

	}
}
