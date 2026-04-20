using Azure.Identity;
using PurpleDepot.Providers.Azure.Options;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using PurpleDepot.Core.Interface.Model;
using PurpleDepot.Core.Interface.Storage;
using PurpleDepot.Core.Interface.Storage.Exceptions;
using Microsoft.Azure.Storage;
using Microsoft.Extensions.Options;

namespace PurpleDepot.Providers.Azure.Storage;
public class AzureStorageService<T> : IStorageProvider<T>
	where T : RegistryItem<T>
{
	private readonly StorageOptions _options;

	public AzureStorageService(IOptions<StorageOptions> options)
		=> _options = options.Value;

	private BlobClient GetBlobClient(string fileKey)
	{
		NameValidator.ValidateContainerName(_options.Container);
		NameValidator.ValidateBlobName(fileKey);

		var credential = new DefaultAzureCredential();
		var containerClient = new BlobContainerClient(_options.BlobClientUrl, credential);
		var blobClient = containerClient.GetBlobClient(fileKey);
		return blobClient;
	}

	public Uri DownloadLink(string fileKey) => GetBlobClient(fileKey).Uri;

	public async Task<(Stream? Stream, long? ContentLength)> DownloadZipAsync(string fileKey)
	{
		var client = GetBlobClient(fileKey);
		var exists = await client.ExistsAsync();
		if (!exists) return (null, null);
		BlobDownloadInfo download = await client.DownloadAsync();
		return (download.Content, download.ContentLength);
	}

	public async Task UploadZipAsync(string fileKey, Stream stream)
	{
		var client = GetBlobClient(fileKey);
		var header = new BlobHttpHeaders { ContentType = "application/zip" };
		await client.UploadAsync(stream, header);
		stream.Close();

		var properties = (await client.GetPropertiesAsync()).Value;
		if (properties.ContentLength == 0)
		{
			await client.DeleteAsync(DeleteSnapshotsOption.IncludeSnapshots);
			throw new FileEmpty(fileKey);
		}
	}
}
