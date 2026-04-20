using System.Net;
using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using PurpleDepot.Core.Controller;
using PurpleDepot.Core.Controller.Data;
using PurpleDepot.Core.Controller.Exceptions;
using PurpleDepot.Core.Interface.Model;
using PurpleDepot.Core.Interface.Model.Provider;
using PurpleDepot.Core.Interface.Storage;
using Microsoft.EntityFrameworkCore;
using AppContext = PurpleDepot.Core.Controller.Data.AppContext;

namespace PurpleDepot.Tests;

public class ProviderControllerTests
{
	[Fact]
	public async Task DownloadPackageAsync_ReturnsACompliantProviderPackagePayload()
	{
		var signer = new FakeProviderPackageSigner();
		var zipBytes = "provider-binary"u8.ToArray();
		var setup = await CreateControllerAsync(signer);
		var controller = setup.Controller;
		var provider = setup.Provider;
		var version = setup.Version;
		var platform = setup.Platform;
		var expectedSha = GetSha256(zipBytes);

		var requestUri = new Uri($"https://registry.example.test/v1/providers/{provider.Namespace}/{provider.Name}/{version.Version}/download/{platform.OS}/{platform.Arch}");
		var result = await controller.ExposeDownloadPackageAsync(
			Provider.GetAddress(provider.Namespace, provider.Name),
			version.Version,
			platform.OS,
			platform.Arch,
			requestUri);

		result.StatusCode.Should().Be(HttpStatusCode.OK);
		var package = result.Content.Should().BeOfType<ProviderPackage>().Subject;
		package.Protocols.Should().Equal(version.Protocols);
		package.OS.Should().Be(platform.OS);
		package.Arch.Should().Be(platform.Arch);
		package.FileName.Should().Be("terraform-provider-widget_1.2.3_linux_amd64.zip");
		package.DownloadUrl.AbsoluteUri.Should().Be($"https://registry.example.test/v1/archive/{MockStorageService<Provider>.EncodePath(provider.GetPackageFileKey(version, platform))}");
		package.ShaSumsUrl.AbsoluteUri.Should().Be($"{requestUri.AbsoluteUri}/SHA256SUMS");
		package.ShaSumsSignatureUrl.AbsoluteUri.Should().Be($"{requestUri.AbsoluteUri}/SHA256SUMS.sig");
		package.ShaSum.Should().Be(expectedSha);
		package.SigningKeys.GpgPublicKeys.Should().ContainSingle();
	}

	[Fact]
	public async Task GetChecksumsAsync_ReturnsSha256SumDocument()
	{
		var signer = new FakeProviderPackageSigner();
		var zipBytes = "provider-binary"u8.ToArray();
		var setup = await CreateControllerAsync(signer);
		var controller = setup.Controller;
		var provider = setup.Provider;
		var version = setup.Version;
		var platform = setup.Platform;
		var expectedSha = GetSha256(zipBytes);

		var result = await controller.ExposeGetChecksumsAsync(
			Provider.GetAddress(provider.Namespace, provider.Name),
			version.Version,
			platform.OS,
			platform.Arch,
			new Uri($"https://registry.example.test/v1/providers/{provider.Namespace}/{provider.Name}/{version.Version}/download/{platform.OS}/{platform.Arch}"));

		result.StatusCode.Should().Be(HttpStatusCode.OK);
		result.Content.Should().Be($"{expectedSha}  terraform-provider-widget_1.2.3_linux_amd64.zip\n");
	}

	[Fact]
	public async Task GetChecksumsSignatureAsync_ReturnsDetachedSignatureBytes()
	{
		var signer = new FakeProviderPackageSigner();
		var zipBytes = "provider-binary"u8.ToArray();
		var setup = await CreateControllerAsync(signer);
		var controller = setup.Controller;
		var provider = setup.Provider;
		var version = setup.Version;
		var platform = setup.Platform;
		var expectedSha = GetSha256(zipBytes);
		var expectedContent = $"{expectedSha}  terraform-provider-widget_1.2.3_linux_amd64.zip\n";

		var result = await controller.ExposeGetChecksumsSignatureAsync(
			Provider.GetAddress(provider.Namespace, provider.Name),
			version.Version,
			platform.OS,
			platform.Arch,
			new Uri($"https://registry.example.test/v1/providers/{provider.Namespace}/{provider.Name}/{version.Version}/download/{platform.OS}/{platform.Arch}"));

		result.StatusCode.Should().Be(HttpStatusCode.OK);
		result.Content.Should().BeEquivalentTo(Encoding.UTF8.GetBytes($"signed:{expectedContent}"));
		signer.LastSignedContent.Should().Be(expectedContent);
	}

	[Fact]
	public async Task DownloadPackageAsync_ReturnsNotFoundForUnknownPlatform()
	{
		var signer = new FakeProviderPackageSigner();
		var setup = await CreateControllerAsync(signer);
		var controller = setup.Controller;
		var provider = setup.Provider;
		var version = setup.Version;

		var act = () => controller.ExposeDownloadPackageAsync(
			Provider.GetAddress(provider.Namespace, provider.Name),
			version.Version,
			"darwin",
			"arm64",
			new Uri($"https://registry.example.test/v1/providers/{provider.Namespace}/{provider.Name}/{version.Version}/download/darwin/arm64"));

		var exception = await act.Should().ThrowAsync<ControllerResultException>();
		exception.Which.Response.StatusCode.Should().Be(HttpStatusCode.NotFound);
	}

	[Fact]
	public async Task DownloadPackageAsync_ReturnsServerErrorWhenProtocolsAreMissing()
	{
		var signer = new FakeProviderPackageSigner();
		var platform = new ProviderPlatform("linux", "amd64");
		var version = new ProviderVersion("1.2.3", protocols: [], platforms: [platform]);
		var uniqueNamespace = $"acme-{Guid.NewGuid():N}";
		var provider = new Provider($"{uniqueNamespace}/widget", uniqueNamespace, "widget", [version], DateTime.UtcNow);
		var controller = await CreateControllerAsync(signer, provider, version, platform, [1, 2, 3]);

		var act = () => controller.ExposeDownloadPackageAsync(
			Provider.GetAddress(provider.Namespace, provider.Name),
			version.Version,
			platform.OS,
			platform.Arch,
			new Uri($"https://registry.example.test/v1/providers/{provider.Namespace}/{provider.Name}/{version.Version}/download/{platform.OS}/{platform.Arch}"));

		var exception = await act.Should().ThrowAsync<ControllerResultException>();
		exception.Which.Response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
		exception.Which.Response.Content.Should().Be($"Provider '{provider.Namespace}/{provider.Name}' version '{version.Version}' is missing required protocol metadata.");
	}

	[Fact]
	public async Task IngestPackageAsync_CreatesProviderVersionAndPlatformMetadata()
	{
		var controller = CreateEmptyController();
		var address = NewProviderAddress();
		var payload = Encoding.UTF8.GetBytes("provider-package");

		var result = await controller.ExposeIngestPackageAsync(
			address,
			"1.2.3",
			"linux",
			"amd64",
			["5.0", "6.0"],
			new MemoryStream(payload));

		result.StatusCode.Should().Be(HttpStatusCode.Created);

		var package = (await controller.ExposeDownloadPackageAsync(
			address,
			"1.2.3",
			"linux",
			"amd64",
			new Uri($"https://registry.example.test/v1/providers/{address.Namespace}/{address.Name}/1.2.3/download/linux/amd64"))).Content
			.Should().BeOfType<ProviderPackage>().Subject;

		package.Protocols.Should().Equal("5.0", "6.0");
		package.DownloadUrl.AbsoluteUri.Should().Be($"https://registry.example.test/v1/archive/{MockStorageService<Provider>.EncodePath($"providers/{address.Namespace}/{address.Name}/1.2.3/linux/amd64/terraform-provider-widget_1.2.3_linux_amd64.zip")}");
	}

	[Fact]
	public async Task IngestPackageAsync_RejectsMissingProtocols()
	{
		var controller = CreateEmptyController();
		var address = NewProviderAddress();

		var act = () => controller.ExposeIngestPackageAsync(
			address,
			"1.2.3",
			"linux",
			"amd64",
			Array.Empty<string>(),
			new MemoryStream([1, 2, 3]));

		var exception = await act.Should().ThrowAsync<ControllerResultException>();
		exception.Which.Response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
	}

	[Fact]
	public async Task IngestPackageAsync_RejectsDuplicatePlatformUploads()
	{
		var controller = CreateEmptyController();
		var address = NewProviderAddress();
		await controller.ExposeIngestPackageAsync(
			address,
			"1.2.3",
			"linux",
			"amd64",
			["5.0"],
			new MemoryStream([1, 2, 3]));

		var act = () => controller.ExposeIngestPackageAsync(
			address,
			"1.2.3",
			"linux",
			"amd64",
			["5.0"],
			new MemoryStream([4, 5, 6]));

		var exception = await act.Should().ThrowAsync<ControllerResultException>();
		exception.Which.Response.StatusCode.Should().Be(HttpStatusCode.Conflict);
	}

	private static async Task<(TestProviderController Controller, Provider Provider, ProviderVersion Version, ProviderPlatform Platform)> CreateControllerAsync(
		FakeProviderPackageSigner signer)
	{
		var platform = new ProviderPlatform("linux", "amd64");
		var version = new ProviderVersion("1.2.3", protocols: ["5.0", "6.0"], platforms: [platform]);
		var uniqueNamespace = $"acme-{Guid.NewGuid():N}";
		var provider = new Provider($"{uniqueNamespace}/widget", uniqueNamespace, "widget", [version], DateTime.UtcNow);
		var controller = await CreateControllerAsync(signer, provider, version, platform, "provider-binary"u8.ToArray());
		return (controller, provider, version, platform);
	}

	private static async Task<TestProviderController> CreateControllerAsync(
		FakeProviderPackageSigner signer,
		Provider provider,
		ProviderVersion version,
		ProviderPlatform platform,
		byte[] zipBytes)
	{
		var options = new DbContextOptionsBuilder<AppContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
			.Options;
		var dbContext = new AppContext(options);
		var repository = new Repository<Provider>(dbContext);
		repository.Add(provider);
		repository.SaveChanges();

		var storage = new MockStorageService<Provider>();
		await storage.UploadZipAsync(provider.GetPackageFileKey(version, platform), new MemoryStream(zipBytes));
		return new TestProviderController(repository, storage, signer);
	}

	private static TestProviderController CreateEmptyController()
	{
		var options = new DbContextOptionsBuilder<AppContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
			.Options;
		var repository = new Repository<Provider>(new AppContext(options));
		return new TestProviderController(repository, new MockStorageService<Provider>(), new FakeProviderPackageSigner());
	}

	private static string GetSha256(byte[] bytes)
		=> Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

	private static ProviderAddress NewProviderAddress()
		=> new($"acme-{Guid.NewGuid():N}", "widget");

	private sealed class TestProviderController : ProviderController
	{
		public TestProviderController(
			IRepository<Provider> itemRepo,
			IStorageProvider<Provider> storageProvider,
			IProviderPackageSigner signer)
			: base(itemRepo, storageProvider, signer) { }

		public Task<ControllerResult> ExposeDownloadPackageAsync(Address<Provider> address, string version, string os, string arch, Uri requestUri)
			=> base.DownloadPackageAsync(address, version, os, arch, requestUri);

		public Task<ControllerResult> ExposeGetChecksumsAsync(Address<Provider> address, string version, string os, string arch, Uri requestUri)
			=> base.GetChecksumsAsync(address, version, os, arch, requestUri);

		public Task<ControllerResult> ExposeGetChecksumsSignatureAsync(Address<Provider> address, string version, string os, string arch, Uri requestUri)
			=> base.GetChecksumsSignatureAsync(address, version, os, arch, requestUri);

		public Task<ControllerResult> ExposeIngestPackageAsync(Address<Provider> address, string version, string os, string arch, IEnumerable<string> protocols, Stream stream)
			=> base.IngestPackageAsync(address, version, os, arch, protocols, stream);
	}

	private sealed class FakeProviderPackageSigner : IProviderPackageSigner
	{
		public string? LastSignedContent { get; private set; }

		public SigningKeys GetSigningKeys()
			=> new([new GpgPublicKey("ABCD1234EF567890", "-----BEGIN PGP PUBLIC KEY BLOCK-----\nfake\n-----END PGP PUBLIC KEY BLOCK-----")]);

		public Task<byte[]> SignAsync(byte[] content)
		{
			LastSignedContent = Encoding.UTF8.GetString(content);
			return Task.FromResult(Encoding.UTF8.GetBytes($"signed:{LastSignedContent}"));
		}
	}
}
