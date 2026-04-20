using PurpleDepot.Core.Interface.Model;
using PurpleDepot.Core.Interface.Storage.Exceptions;

namespace PurpleDepot.Core.Interface.Storage;
public class MockStorageService<T> : IStorageProvider<T> where T: RegistryItem<T>
{
	private static readonly Dictionary<string, byte[]> _files = new();
	public Uri DownloadLink(string fileKey)
	{
		if (!_files.ContainsKey(fileKey))
			throw new FileNotFound(fileKey);

		return new Uri($"https://mock-storage.invalid/{Uri.EscapeDataString(fileKey)}");
	}

	public async Task<(Stream? Stream, long? ContentLength)> DownloadZipAsync(string fileKey)
	{
		if (!_files.ContainsKey(fileKey))
			throw new FileNotFound(fileKey);

		return await Task.Run(() =>
		{
			var bytes = _files[fileKey];
			return (new MemoryStream(bytes), bytes.Length);
		});
	}

	public async Task UploadZipAsync(string fileKey, Stream stream)
	{
		if(_files.ContainsKey(fileKey))
			throw new FileAlreadyExists(fileKey);

		using var buffer = new MemoryStream();
		await stream.CopyToAsync(buffer);
		var bytes = buffer.ToArray();

		if (bytes.Length == 0)
			throw new FileEmpty(fileKey);

		_files.Add(fileKey, bytes);
	}
}
