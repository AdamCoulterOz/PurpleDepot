using System.Net;
using System.Web;
using PurpleDepot.Core.Controller.Data;
using PurpleDepot.Core.Controller.Exceptions;
using PurpleDepot.Core.Interface.Model;
using PurpleDepot.Core.Interface.Storage;

namespace PurpleDepot.Core.Controller;

public class ItemController<T>
	where T : RegistryItem<T>
{
	private const string ArchiveRouteRoot = "v1/archive";
	protected readonly IRepository<T> ItemRepo;
	protected readonly IStorageProvider<T> StorageProvider;

	protected ItemController(IRepository<T> itemRepo, IStorageProvider<T> storageProvider)
	{
		ItemRepo = itemRepo;
		ItemRepo.EnsureCreated();
		StorageProvider = storageProvider;
	}

	protected async Task<ControllerResult> IngestAsync(Address<T> newAddress,
		string version, Stream stream)
	{
		var item = await ItemRepo.GetItemAsync(newAddress);

		if (item is not null && item.HasVersion(version))
			throw new AlreadyExistsException<T>(newAddress);

		if (item is null)
		{
			item = newAddress.NewItem(version);
			ItemRepo.Add(item);
		}
		else
			item.AddVersion(version);

		var newVersion = item.GetVersion(version)!;
		try
		{
			await StorageProvider.UploadZipAsync(item.GetFileKey(newVersion), stream);
		}
		catch (Exception e)
		{
			throw new ControllerResultException(HttpStatusCode.InternalServerError, $"Uploading file failed, and the module was not saved. Inner error: {e.Message}");
		}

		ItemRepo.SaveChanges();
		return ControllerResult.New(HttpStatusCode.Created);
	}

	protected async Task<ControllerResult> DownloadAsync(Address<T> itemId,
		string? versionName = null,
		Uri? requestUri = null)
	{
		var (item, version) = await GetItemAsync(itemId, versionName);
		var fileKey = item.GetFileKey(version);

		var response = ControllerResult.New(HttpStatusCode.NoContent);
		var downloadUri = ResolveDownloadUri(fileKey, requestUri);
		var builder = new UriBuilder(downloadUri);
		var query = HttpUtility.ParseQueryString(builder.Query);
		query.Add("archive", "zip");
		builder.Query = query.ToString();
		if (downloadUri.IsDefaultPort)
			builder.Port = -1;
		response.AddHeader("X-Terraform-Get", builder.ToString());
		return response;
	}

	protected virtual async Task<ControllerResult> GetAsync(Address<T> itemId, string? versionName = null)
		=> ControllerResult.NewJson((await GetItemAsync(itemId, versionName)).item);

	protected async Task<(T item, RegistryItemVersion version)> GetItemAsync(
		Address<T> itemId, string? versionName = null)
	{
		var item = await ItemRepo.GetItemAsync(itemId);
		var version = item?.GetVersion(versionName);
		if (item is null || version is null)
			throw new NotFoundException<T>(itemId);
		return (item, version);
	}

	protected Uri ResolveDownloadUri(string fileKey, Uri? requestUri = null)
	{
		var downloadUri = StorageProvider.DownloadLink(fileKey);
		if (downloadUri.Scheme != MockStorageService<T>.DownloadScheme || requestUri is null)
			return downloadUri;

		return new Uri($"{requestUri.Scheme}://{requestUri.Authority}/{ArchiveRouteRoot}/{MockStorageService<T>.EncodePath(fileKey)}");
	}
}
