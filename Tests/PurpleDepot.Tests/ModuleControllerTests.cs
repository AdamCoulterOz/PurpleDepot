using System.Net;
using FluentAssertions;
using PurpleDepot.Core.Controller;
using PurpleDepot.Core.Controller.Data;
using PurpleDepot.Core.Interface.Model.Module;
using PurpleDepot.Core.Interface.Storage;
using Microsoft.EntityFrameworkCore;
using AppContext = PurpleDepot.Core.Controller.Data.AppContext;

namespace PurpleDepot.Tests;

public class ModuleControllerTests
{
	[Fact]
	public async Task DownloadAsync_UsesHostedArchiveUrlWhenBackedByMockStorage()
	{
		var controller = await CreateControllerAsync();

		var result = await controller.ExposeDownloadAsync(
			new ModuleAddress("acme", "widget", "azurerm"),
			"1.2.3",
			new Uri("https://registry.example.test/v1/modules/acme/widget/azurerm/1.2.3/download"));

		result.StatusCode.Should().Be(HttpStatusCode.NoContent);
		result.EnumerableHeaders.Should().ContainSingle(header => header.Key == "X-Terraform-Get");
		result.EnumerableHeaders.Single(header => header.Key == "X-Terraform-Get").Value.Single()
			.Should().Be($"https://registry.example.test/v1/archive/{MockStorageService<Module>.EncodePath("modules/acme/widget/azurerm/21594df4-48f3-4f9b-b1ff-8e18fb4f4a7d-1.2.3.zip")}?archive=zip");
	}

	private static async Task<TestModuleController> CreateControllerAsync()
	{
		var version = new ModuleVersion("1.2.3", Guid.Parse("21594df4-48f3-4f9b-b1ff-8e18fb4f4a7d"));
		var module = new Module("acme/widget/azurerm", "acme", "widget", [version], "azurerm", ["azurerm"], DateTime.UtcNow);
		var options = new DbContextOptionsBuilder<AppContext>()
			.UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
			.Options;
		var repository = new Repository<Module>(new AppContext(options));
		repository.Add(module);
		repository.SaveChanges();

		var storage = new MockStorageService<Module>();
		await storage.UploadZipAsync(module.GetFileKey(version), new MemoryStream([1, 2, 3]));
		return new TestModuleController(repository, storage);
	}

	private sealed class TestModuleController : ModuleController
	{
		public TestModuleController(IRepository<Module> itemRepo, IStorageProvider<Module> storageProvider)
			: base(itemRepo, storageProvider) { }

		public Task<ControllerResult> ExposeDownloadAsync(ModuleAddress address, string version, Uri requestUri)
			=> base.DownloadAsync(address, version, requestUri);
	}
}
