using System.Text;
using FluentAssertions;
using PurpleDepot.Core.Interface.Model.Module;
using PurpleDepot.Core.Interface.Storage;
using PurpleDepot.Core.Interface.Storage.Exceptions;

namespace PurpleDepot.Tests;

public class MockStorageServiceTests
{
	[Fact]
	public async Task UploadAndDownloadZipAsync_RoundTripsBytes()
	{
		var storage = new MockStorageService<Module>();
		var fileKey = UniqueKey();
		var bytes = Encoding.UTF8.GetBytes("hello registry");

		await storage.UploadZipAsync(fileKey, new MemoryStream(bytes));
		var downloadUri = storage.DownloadLink(fileKey);
		var (stream, contentLength) = await storage.DownloadZipAsync(fileKey);

		downloadUri.AbsoluteUri.Should().Be($"https://mock-storage.invalid/{Uri.EscapeDataString(fileKey)}");
		contentLength.Should().Be(bytes.Length);
		stream.Should().NotBeNull();

		using var reader = new MemoryStream();
		await stream!.CopyToAsync(reader);
		reader.ToArray().Should().Equal(bytes);
	}

	[Fact]
	public async Task UploadZipAsync_ThrowsWhenTheStreamIsEmpty()
	{
		var storage = new MockStorageService<Module>();

		var act = () => storage.UploadZipAsync(UniqueKey(), Stream.Null);

		await act.Should().ThrowAsync<FileEmpty>();
	}

	[Fact]
	public async Task UploadZipAsync_ThrowsWhenTheFileAlreadyExists()
	{
		var storage = new MockStorageService<Module>();
		var fileKey = UniqueKey();
		await storage.UploadZipAsync(fileKey, new MemoryStream([1, 2, 3]));

		var act = () => storage.UploadZipAsync(fileKey, new MemoryStream([4, 5, 6]));

		await act.Should().ThrowAsync<FileAlreadyExists>();
	}

	[Fact]
	public async Task DownloadZipAsync_ThrowsWhenTheFileDoesNotExist()
	{
		var storage = new MockStorageService<Module>();

		var act = () => storage.DownloadZipAsync(UniqueKey());

		await act.Should().ThrowAsync<FileNotFound>();
	}

	[Fact]
	public void DownloadLink_ThrowsWhenTheFileDoesNotExist()
	{
		var storage = new MockStorageService<Module>();

		var act = () => storage.DownloadLink(UniqueKey());

		act.Should().Throw<FileNotFound>();
	}

	private static string UniqueKey() => $"modules/test/{Guid.NewGuid():N}.zip";
}
