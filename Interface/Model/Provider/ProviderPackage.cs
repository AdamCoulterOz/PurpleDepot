using System.Text.Json.Serialization;

namespace PurpleDepot.Interface.Model.Provider;

public class ProviderPackage
{
	[JsonPropertyName("protocols")]
	public List<string> Protocols { get; }

	[JsonPropertyName("os")]
	public string OS { get; }

	[JsonPropertyName("arch")]
	public string Arch { get; }

	[JsonPropertyName("filename")]
	public string FileName { get; }

	[JsonPropertyName("download_url")]
	public Uri DownloadUrl { get; }

	[JsonPropertyName("shasums_url")]
	public Uri ShaSumsUrl { get; }

	[JsonPropertyName("shasums_signature_url")]
	public Uri ShaSumsSignatureUrl { get; }

	[JsonPropertyName("shasum")]
	public string ShaSum { get; }

	[JsonPropertyName("signing_keys")]
	public SigningKeys SigningKeys { get; }

	[JsonConstructor]
	public ProviderPackage(List<string> protocols, string os, string arch, string filename, Uri download_url, Uri shasums_url, Uri shasums_signature_url, string shasum, SigningKeys signing_keys)
		=> (Protocols, OS, Arch, FileName, DownloadUrl, ShaSumsUrl, ShaSumsSignatureUrl, ShaSum, SigningKeys) = (protocols, os, arch, filename, download_url, shasums_url, shasums_signature_url, shasum, signing_keys);

	public static ProviderPackage NewFromProvider(Provider provider, ProviderVersion version, ProviderPlatform platform)
	{
		throw new NotImplementedException();
		// new ProviderPackage(
		// 	protocols: version.Protocols,
		// 	os: platform.OS,
		// 	arch: platform.Arch,
		// 	filename: ,
		// 	download_url: ,
		// 	shasums_url: ,
		// 	shasums_signature_url: ,
		// 	shasum: ,
		// 	signing_keys:
		// );
	}

}