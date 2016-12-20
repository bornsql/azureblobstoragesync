using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace RemoteStorageHelper
{
	public static class ConsoleHelper
	{
		public static string Header()
		{
			var header = new StringBuilder();
			var s =
				$"{Process.GetCurrentProcess().ProcessName} version {FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).ProductVersion}";
			header.AppendLine(s);
			s = "Copyright (c) Randolph West (bornsql.ca).";
			header.AppendLine(s);
			return header.ToString();
		}
	}
}
