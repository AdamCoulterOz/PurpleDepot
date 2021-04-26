using System;
using System.IO;
using System.Threading.Tasks;

namespace PurpleDepot.Controller.Storage
{
	public class MockStorageService : IStorageProvider
	{
		public Stream DownloadFile(Guid fileKey)
		{
			var file = "asfhjklasfghjkalsfd";
			return new MemoryStream(Convert.ToByte(file));
		}

		public async Task UploadFile(Guid fileKey, Stream stream)
		{
			using var sr = new StreamReader(stream);
			await sr.ReadToEndAsync();
		}
	}
}