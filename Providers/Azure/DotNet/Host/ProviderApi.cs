using PurpleDepot.Core.Controller;
using PurpleDepot.Core.Controller.Data;
using PurpleDepot.Core.Interface.Model.Provider;
using PurpleDepot.Core.Interface.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace PurpleDepot.Providers.Azure.Host;

public class ProviderApi : ProviderController
{
	public ProviderApi(IRepository<Provider> repo, IStorageProvider<Provider> storageProvider) : base(repo, storageProvider) { }

	[Function($"{nameof(ProviderApi)}_{nameof(Versions)}")]
	public async Task<HttpResponseData> Versions(
		[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ProviderRoutes.Versions)]
			HttpRequestData request, string @namespace, string name)
				=> await request.CreateResponseAsync(async () => await GetAsync(new ProviderAddress(@namespace, name)));

	[Function($"{nameof(ProviderApi)}_{nameof(Download)}")]
	public async Task<HttpResponseData> Download(
		[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ProviderRoutes.Download)]
			HttpRequestData request, string @namespace, string name)
				=> await request.CreateResponseAsync(async () => await DownloadAsync(new ProviderAddress(@namespace, name)));

	[Function($"{nameof(ProviderApi)}_{nameof(DownloadVersion)}")]
	public async Task<HttpResponseData> DownloadVersion(
		[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ProviderRoutes.DownloadVersion)]
			HttpRequestData request, string @namespace, string name, string version)
			=> await request.CreateResponseAsync(async () => await DownloadAsync(new ProviderAddress(@namespace, name), version));

	[Function($"{nameof(ProviderApi)}_{nameof(Latest)}")]
	public async Task<HttpResponseData> Latest(
		[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ProviderRoutes.Latest)]
			HttpRequestData request, string @namespace, string name)
			=> await request.CreateResponseAsync(async () => await GetAsync(new ProviderAddress(@namespace, name)));

	[Function($"{nameof(ProviderApi)}_{nameof(Version)}")]
	public async Task<HttpResponseData> Version(
		[HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = ProviderRoutes.Version)]
			HttpRequestData request,
		string @namespace, string name, string version)
	{
		if (version == "versions")
			return await Versions(request, @namespace, name);
		return await request.CreateResponseAsync(async () => await GetAsync(new ProviderAddress(@namespace, name), version));
	}

	[Function($"{nameof(ProviderApi)}_{nameof(Ingest)}")]
	public async Task<HttpResponseData> Ingest(
		[HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = ProviderRoutes.Ingest)]
			HttpRequestData request,
		string @namespace, string name, string version)
			=> await request.CreateResponseAsync(async () => await IngestAsync(new ProviderAddress(@namespace, name), version, request.Body));
}
