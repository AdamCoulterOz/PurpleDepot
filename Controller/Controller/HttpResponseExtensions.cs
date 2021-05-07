using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.IO;
using System.Net.Http.Headers;

namespace PurpleDepot.Controller
{
	public static class HttpResponseExtensions
	{
		public static HttpResponseMessage CreateResponse(this HttpRequestMessage request, HttpStatusCode statusCode)
		{
			return new HttpResponseMessage(statusCode)
			{
				RequestMessage = request
			};
		}
		public static HttpResponseMessage CreateStringResponse(this HttpRequestMessage requestMessage, HttpStatusCode statusCode, string message)
		{
			var response = requestMessage.CreateResponse(statusCode);
			response.Content = new StringContent(message);
			return response;
		}
		public static HttpResponseMessage CreateJsonResponse(this HttpRequestMessage request, object document)
		{
			return new HttpResponseMessage(HttpStatusCode.OK)
			{
				RequestMessage = request,
				Content = JsonContent.Create(document)
			};
		}
		public static HttpResponseMessage CreateZipDownloadResponse(this HttpRequestMessage request, Stream downloadStream, string fileName)
		{
			return request.CreateDownloadResponse(downloadStream, fileName, new MediaTypeHeaderValue("application/zip"));
		}
		public static HttpResponseMessage CreateDownloadResponse(this HttpRequestMessage request, Stream downloadStream, string fileName, MediaTypeHeaderValue mediaType)
		{
			var response = request.CreateResponse(HttpStatusCode.OK);
			response.Content = new StreamContent(downloadStream);
			response.Content.Headers.ContentType = mediaType;
			response.Content.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment")
			{
				FileName = fileName
			};
			return response;
		}
	}
}