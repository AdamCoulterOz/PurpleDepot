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

	protected async Task<ControllerResult> IngestPackageAsync(
		Address<Provider> itemId,
		string versionName,
		string os,
		string arch,
		IEnumerable<string> protocols,
		Stream stream)
	{
		var normalizedProtocols = protocols
			.Select(protocol => protocol.Trim())
			.Where(protocol => !string.IsNullOrWhiteSpace(protocol))
			.Distinct(StringComparer.Ordinal)
			.ToList();

		if (!normalizedProtocols.Any())
			throw new ControllerResultException(HttpStatusCode.BadRequest, "Provider package upload requires at least one protocol version via the X-Terraform-Protocols header.");

		var normalizedPlatform = new ProviderPlatform(os.ToLowerInvariant(), arch.ToLowerInvariant());
		var provider = await ItemRepo.GetItemAsync(itemId);
		ProviderVersion version;

		if (provider is null)
		{
			provider = itemId.NewItem(versionName);
			ItemRepo.Add(provider);
			version = provider.GetVersion(versionName) as ProviderVersion
				?? throw new ControllerResultException(HttpStatusCode.InternalServerError, $"Provider version '{versionName}' could not be created.");
			version.Protocols = normalizedProtocols;
			version.Platforms = [normalizedPlatform];
		}
		else
		{
			if (provider.GetVersion(versionName) is ProviderVersion existingVersion)
			{
				version = existingVersion;
				if (version.Platforms?.Any(platform =>
					string.Equals(platform.OS, normalizedPlatform.OS, StringComparison.OrdinalIgnoreCase)
					&& string.Equals(platform.Arch, normalizedPlatform.Arch, StringComparison.OrdinalIgnoreCase)) == true)
				{
					throw new ControllerResultException(HttpStatusCode.Conflict, $"Provider package already exists for '{itemId}' version '{versionName}' on '{normalizedPlatform.OS}/{normalizedPlatform.Arch}'.");
				}

				version.Protocols = version.Protocols
					.Concat(normalizedProtocols)
					.Distinct(StringComparer.Ordinal)
					.ToList();
				version.Platforms ??= [];
				version.Platforms.Add(normalizedPlatform);
			}
			else
			{
				version = provider.AddVersion(new ProviderVersion(versionName, normalizedProtocols, [normalizedPlatform])) as ProviderVersion
					?? throw new ControllerResultException(HttpStatusCode.InternalServerError, $"Provider version '{versionName}' could not be created.");
			}
		}

		try
		{
			await StorageProvider.UploadZipAsync(provider.GetPackageFileKey(version, normalizedPlatform), stream);
		}
		catch (Core.Interface.Storage.Exceptions.FileAlreadyExists)
		{
			throw new ControllerResultException(HttpStatusCode.Conflict, $"Provider package already exists for '{itemId}' version '{versionName}' on '{normalizedPlatform.OS}/{normalizedPlatform.Arch}'.");
		}
		catch (Exception e)
		{
			throw new ControllerResultException(HttpStatusCode.InternalServerError, $"Uploading file failed, and the provider package was not saved. Inner error: {e.Message}");
		}

		ItemRepo.SaveChanges();
		return ControllerResult.New(HttpStatusCode.Created);
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
		var downloadUrl = ResolveDownloadUri(fileKey, requestUri);

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
