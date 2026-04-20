using System.Net;
using PurpleDepot.Core.Controller;
using PurpleDepot.Core.Controller.Exceptions;
using PurpleDepot.Core.Interface.Model.Module;
using PurpleDepot.Core.Interface.Model.Provider;
using PurpleDepot.Core.Interface.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace PurpleDepot.Providers.Azure.Host;

public class ArtifactApi
{
	public const string Route = "v1/archive/{*fileKey}";

	private readonly IStorageProvider<Module> _moduleStorageProvider;
	private readonly IStorageProvider<Provider> _providerStorageProvider;

	public ArtifactApi(
		IStorageProvider<Module> moduleStorageProvider,
		IStorageProvider<Provider> providerStorageProvider)
	{
		_moduleStorageProvider = moduleStorageProvider;
		_providerStorageProvider = providerStorageProvider;
	}

	[Function(nameof(DownloadArchiveAsync))]
	public async Task<HttpResponseData> DownloadArchiveAsync(
		[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = Route)] HttpRequestData request,
		string fileKey)
		=> await request.CreateResponseAsync(async () =>
		{
			var normalizedKey = NormalizeFileKey(fileKey);
			if (normalizedKey.StartsWith("modules/", StringComparison.Ordinal))
				return await DownloadArchiveAsync(_moduleStorageProvider, normalizedKey);

			if (normalizedKey.StartsWith("providers/", StringComparison.Ordinal))
				return await DownloadArchiveAsync(_providerStorageProvider, normalizedKey);

			throw new ControllerResultException(HttpStatusCode.NotFound, $"Archive '{normalizedKey}' was not found.");
		});

	private static string NormalizeFileKey(string fileKey)
		=> string.Join("/", fileKey.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.UnescapeDataString));

	private static async Task<ControllerResult> DownloadArchiveAsync<T>(IStorageProvider<T> storageProvider, string fileKey)
		where T : PurpleDepot.Core.Interface.Model.RegistryItem<T>
	{
		try
		{
			var download = await storageProvider.DownloadZipAsync(fileKey);
			if (download.Stream is null)
				throw new ControllerResultException(HttpStatusCode.NotFound, $"Archive '{fileKey}' was not found.");

			await using (download.Stream)
			using (var buffer = new MemoryStream())
			{
				await download.Stream.CopyToAsync(buffer);
				return ControllerResult.NewBinary(buffer.ToArray(), "application/zip");
			}
		}
		catch (Core.Interface.Storage.Exceptions.FileNotFound)
		{
			throw new ControllerResultException(HttpStatusCode.NotFound, $"Archive '{fileKey}' was not found.");
		}
	}
}
