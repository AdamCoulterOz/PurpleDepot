using System.Net;

namespace PurpleDepot.Core.Controller;

public class ControllerResult
{
	public HttpStatusCode StatusCode { get; set; }
	private Dictionary<string, List<string>> Headers { get; set; } = new();
	public object? Content { get; set; }

	public IEnumerable<KeyValuePair<string, IEnumerable<string>>> EnumerableHeaders
		=> Headers.Select(header => new KeyValuePair<string, IEnumerable<string>>(header.Key, header.Value));

	private ControllerResult(HttpStatusCode statusCode, object? content = null)
	{
		StatusCode = statusCode;
		Content = content;
	}

	public void AddHeader(string name, string value)
	{
		if (Headers.ContainsKey(name))
			Headers[name].Add(value);
		else
			Headers.Add(name, new List<string> { value });
	}

	public static ControllerResult New(HttpStatusCode statusCode)
		=> new ControllerResult(statusCode);

	public static ControllerResult New(HttpStatusCode statusCode, string message)
		=> new ControllerResult(statusCode, message);

	public static ControllerResult NewBinary(byte[] content, string contentType)
	{
		var result = new ControllerResult(HttpStatusCode.OK, content);
		result.AddHeader("Content-Type", contentType);
		return result;
	}

	public static ControllerResult NewJson(object content)
		=> new ControllerResult(HttpStatusCode.OK, content);
}
