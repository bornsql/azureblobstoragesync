using System;
using System.Configuration;

namespace AzureBlobStorageHelper
{
	public static class ConfigHelper
	{
		/// <summary>
		/// Gets the Azure connection string.
		/// </summary>
		/// <returns></returns>
		public static string GetAzureConnectionString()
		{
			return ConfigurationManager.ConnectionStrings["AzureConnectionString"].ConnectionString;
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
	}
}
