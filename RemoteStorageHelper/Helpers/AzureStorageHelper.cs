using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using RemoteStorageHelper.Entities;
using RemoteStorageHelper.Enums;

namespace RemoteStorageHelper.Helpers
{
	public class AzureStorageHelper
	{
		public BlobContainerClient Container { get; }
		public BlobServiceClient ServiceClient { get; }

		public AzureStorageHelper(ConnectionEntity connections, ConfigurationEntity config)
		{
			// Get a connection string to our Azure Storage account.
			var connectionString = connections.RemoteStorageConnectionString;
			var container = config.Container;

			// Get a reference to a container named "sample-container" and then create it
			ServiceClient = new BlobServiceClient(connectionString);
			Container = ServiceClient.GetBlobContainerClient(container);
		}

		/// <summary>
		/// Parses the Cloud Storage account for a list of files to restore.
		/// </summary>
		/// <returns></returns>
		public async Task<List<RemoteItemEntity>> GetBlobItemListAsync()
		{
			if (!await Container.ExistsAsync())
			{
				return new List<RemoteItemEntity>();
			}

			Console.WriteLine("Fetching list of items in Remote Storage ...");

			// Loop over items within the container and fetch the blob files
			var blobItems = await ListBlobsFlatListing(Container, 5000);

			Console.Write($"{Environment.NewLine}Retrieved {blobItems.Count} item(s) ...");

			Console.WriteLine();
			return blobItems;
		}

		private static ItemType ParseBlobType(BlobType? blobType)
		{
			if (!blobType.HasValue)
			{
				return ItemType.Unknown;
			}

			return blobType switch
			{
				BlobType.Block => ItemType.Block,
				BlobType.Page => ItemType.Page,
				BlobType.Append => ItemType.Append,
				_ => ItemType.Unknown
			};
		}

		private static async Task<List<RemoteItemEntity>> ListBlobsFlatListing(BlobContainerClient blobContainerClient, int? segmentSize)
		{
			var items = new ConcurrentBag<RemoteItemEntity>();

			try
			{
				// Call the listing operation and return pages of the specified size.
				var resultSegment = blobContainerClient.GetBlobsAsync().AsPages(default, segmentSize);

				// Enumerate the blobs returned for each page.
				await foreach (var blobPage in resultSegment)
				{
					foreach (var blob in blobPage.Values)
					{
						if (blob.Name.Length >= 15)
						{
							items.Add(new RemoteItemEntity
							{
								Name = blob.Name,
								BackupDate = Common.ParseDateFromFileName(blob.Name),
								BackupType = Common.ParseBackupTypeFromFileName(blob.Name),
								Size = blob.Properties.ContentLength ?? 0,
								LastModified = blob.Properties.LastModified,
								Type = ParseBlobType(blob.Properties.BlobType)
							});
						}
					}
				}

				return items.ToList();
			}
			catch
			{
				return new List<RemoteItemEntity>();
			}
		}
	}
}
