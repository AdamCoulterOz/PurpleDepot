using System.Net;
using System.Security.Cryptography;
using System.Text;
using PurpleDepot.Core.Controller.Data;
using PurpleDepot.Core.Controller.Exceptions;
using PurpleDepot.Core.Interface.Model;
using PurpleDepot.Core.Interface.Model.Provider;
using PurpleDepot.Core.Interface.Storage;

namespace PurpleDepot.Core.Controller;
public class ProviderController : ItemController<Provider>
{
	private readonly IProviderPackageSigner _packageSigner;

	protected ProviderController(
		IRepository<Provider> itemRepo,
		IStorageProvider<Provider> storageProvider,
		IProviderPackageSigner packageSigner)
		: base(itemRepo, storageProvider)
			=> _packageSigner = packageSigner;

	protected async Task<ControllerResult> DownloadPackageAsync(
		Address<Provider> itemId,
		string versionName,
		string os,
		string arch,
		Uri requestUri)
	{
		var packageDetails = await GetPackageDetailsAsync(itemId, versionName, os, arch, requestUri);
		return ControllerResult.NewJson(packageDetails.Package);
	}

	protected async Task<ControllerResult> GetChecksumsAsync(
		Address<Provider> itemId,
		string versionName,
		string os,
		string arch,
		Uri requestUri)
	{
		var packageDetails = await GetPackageDetailsAsync(itemId, versionName, os, arch, requestUri);
		var response = ControllerResult.New(HttpStatusCode.OK, packageDetails.ShaSumsContent);
		response.AddHeader("Content-Type", "text/plain; charset=utf-8");
		return response;
	}

	protected async Task<ControllerResult> GetChecksumsSignatureAsync(
		Address<Provider> itemId,
		string versionName,
		string os,
		string arch,
		Uri requestUri)
	{
		var packageDetails = await GetPackageDetailsAsync(itemId, versionName, os, arch, requestUri);
		var signature = await _packageSigner.SignAsync(Encoding.UTF8.GetBytes(packageDetails.ShaSumsContent));
		return ControllerResult.NewBinary(signature, "application/octet-stream");
	}

	private async Task<ProviderPackageDetails> GetPackageDetailsAsync(
		Address<Provider> itemId,
		string versionName,
		string os,
		string arch,
		Uri requestUri)
	{
		var (item, registryVersion) = await GetItemAsync(itemId, versionName);
		if (registryVersion is not ProviderVersion version)
			throw new ControllerResultException(HttpStatusCode.InternalServerError, $"Registry version '{versionName}' is not a provider version.");

		if (!version.Protocols.Any())
			throw new ControllerResultException(HttpStatusCode.InternalServerError, $"Provider '{itemId}' version '{versionName}' is missing required protocol metadata.");

		var platform = version.Platforms?.FirstOrDefault(p =>
			string.Equals(p.OS, os, StringComparison.OrdinalIgnoreCase)
			&& string.Equals(p.Arch, arch, StringComparison.OrdinalIgnoreCase));

		if (platform is null)
			throw new NotFoundException<Provider>(itemId);

		var normalizedPlatform = new ProviderPlatform(platform.OS.ToLowerInvariant(), platform.Arch.ToLowerInvariant());
		var fileKey = item.GetPackageFileKey(version, normalizedPlatform);
		var downloadUrl = StorageProvider.DownloadLink(fileKey);

		string shaSum;
		try
		{
			var (stream, _) = await StorageProvider.DownloadZipAsync(fileKey);
			if (stream is null)
				throw new ControllerResultException(HttpStatusCode.NotFound, $"Provider package '{fileKey}' was not found.");

			await using (stream)
			{
				var hash = await SHA256.HashDataAsync(stream);
				shaSum = Convert.ToHexString(hash).ToLowerInvariant();
			}
		}
		catch (Core.Interface.Storage.Exceptions.FileNotFound)
		{
			throw new ControllerResultException(HttpStatusCode.NotFound, $"Provider package '{fileKey}' was not found.");
		}

		var checksumsUrl = new Uri($"{requestUri.AbsoluteUri.TrimEnd('/')}/SHA256SUMS");
		var checksumsSignatureUrl = new Uri($"{requestUri.AbsoluteUri.TrimEnd('/')}/SHA256SUMS.sig");
		var signingKeys = _packageSigner.GetSigningKeys();
		var package = ProviderPackage.NewFromProvider(
			item,
			version,
			normalizedPlatform,
			downloadUrl,
			checksumsUrl,
			checksumsSignatureUrl,
			shaSum,
			signingKeys);
		var shaSumsContent = $"{shaSum}  {package.FileName}\n";
		return new ProviderPackageDetails(package, shaSumsContent);
	}

	private sealed record ProviderPackageDetails(ProviderPackage Package, string ShaSumsContent);
}
