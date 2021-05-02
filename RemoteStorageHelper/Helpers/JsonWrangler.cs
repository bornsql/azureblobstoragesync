using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace RemoteStorageHelper.Helpers
{
	public static class JsonWrangler
	{
		public static void WriteJsonItem<T>(T item, string outputFile)
		{
			var opt = new JsonSerializerOptions {WriteIndented = true};
			File.WriteAllText(outputFile, JsonSerializer.Serialize(item, opt));
		}

		public static void WriteJsonList<T>(List<T> listOfObjects, string outputFile)
		{
			var opt = new JsonSerializerOptions {WriteIndented = true};
			File.WriteAllText(outputFile, JsonSerializer.Serialize(listOfObjects, opt));
		}

		public static T ReadJsonItem<T>(FileInfo file)
		{
			var reader = File.ReadAllText(file.FullName);
			return JsonSerializer.Deserialize<T>(reader);
		}

		public static List<T> ReadJson<T>(FileInfo file)
		{
			var reader = File.ReadAllText(file.FullName);
			return JsonSerializer.Deserialize<List<T>>(reader);
		}
	}
}
