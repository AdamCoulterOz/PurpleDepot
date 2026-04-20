namespace PurpleDepot.Core.Interface.Model.Provider;

public class ProviderRoutes : IRoutes
{
	public static string RootName => "providers.v1";
	public static string RootPath => Root;


	public const string Root = "v1/providers";
	private const string Common = $"{Root}/{{namespace}}/{{name}}";
	private const string CommonVersion = $"{Common}/{{version}}";


	public const string Versions = $"{Common}/versions";
	public const string Download = $"{Common}/download";
	public const string DownloadVersion = $"{CommonVersion}/download";
	public const string DownloadPlatform = $"{CommonVersion}/download/{{os}}/{{arch}}";
	public const string Checksums = $"{DownloadPlatform}/SHA256SUMS";
	public const string ChecksumsSignature = $"{DownloadPlatform}/SHA256SUMS.sig";
	public const string Latest = $"{Common}";
	public const string Version = $"{CommonVersion}";
	public const string Ingest = $"{CommonVersion}/upload";
	public const string IngestPlatform = $"{CommonVersion}/upload/{{os}}/{{arch}}";
}
