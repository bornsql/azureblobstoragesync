﻿using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace RemoteStorageHelper
{
	public static class ConsoleHelper
	{
		public static string Header(string version)
		{
			var header = new StringBuilder();
			var s =
				$"{Process.GetCurrentProcess().ProcessName} version {version}";
			header.AppendLine(s);
			s = "Copyright (c) Randolph West (bornsql.ca).";
			header.AppendLine(s);
			return header.ToString();
		}
	}
}
