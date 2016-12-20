using System;
using System.Configuration;

namespace RemoteStorageHelper
{
	public static class ConfigHelper
	{
		/// <summary>
		/// Gets the RemoteStorage connection string.
		/// </summary>
		/// <returns></returns>
		public static string GetRemoteStorageConnectionString()
		{
			return ConfigurationManager.ConnectionStrings["RemoteStorageConnectionString"].ConnectionString;
		}

		public static bool GetConfigurationBoolean(string configurationKey)
		{
			try
			{
				return string.Equals(ConfigurationManager.AppSettings[configurationKey], "true",
					StringComparison.InvariantCultureIgnoreCase);
			}
			catch (NullReferenceException)
			{
				return false;
			}
		}

		/// <summary>
		/// Gets the configuration value.
		/// </summary>
		/// <param name="configurationKey">The configuration key.</param>
		/// <returns></returns>
		public static string GetConfigurationValue(string configurationKey)
		{
			return ConfigurationManager.AppSettings[configurationKey];
		}

		/// <summary>
		/// Gets the File Storage connection details.
		/// </summary>
		/// <returns></returns>
		public static string GetFileStorageUsername()
		{
			return ConfigurationManager.ConnectionStrings["FileStorageUserAccount"].ConnectionString;
		}

		/// <summary>
		/// Gets the File Storage connection details.
		/// </summary>
		/// <returns></returns>
		public static string GetFileStoragePassword()
		{
			return ConfigurationManager.ConnectionStrings["FileStoragePassword"].ConnectionString;
		}


	}
}
