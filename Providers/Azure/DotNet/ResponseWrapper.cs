using PurpleDepot.Core.Controller.Exceptions;
using Microsoft.Azure.Functions.Worker.Http;
using PurpleDepot.Core.Controller;

namespace PurpleDepot.Providers.Azure;

public static class ResponseWrapper
{
	private static async Task<HttpResponseData> AsResponseDataAsync(this ControllerResult result, HttpRequestData request)
	{
		var response = request.CreateResponse(result.StatusCode);
		response.Headers = new HttpHeadersCollection(result.EnumerableHeaders);

		var writeTask = result.Content switch
		{
			null => Task.CompletedTask,
			string content => response.WriteStringAsync(content),
			byte[] content => response.Body.WriteAsync(content).AsTask(),
			not null => response.WriteAsJsonAsync(result.Content).AsTask(),
		};
		await writeTask;
		return response;
	}

	public static async Task<HttpResponseData> CreateResponseAsync(this HttpRequestData request, Func<Task<ControllerResult>> controllerFunc)
	{
		try
		{
			var result = await controllerFunc();
			return await result.AsResponseDataAsync(request);
		}
		catch (ControllerResultException re)
		{
			return await re.Response.AsResponseDataAsync(request);
		}
	}
}
