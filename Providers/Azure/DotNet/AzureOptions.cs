namespace PurpleDepot.Providers.Azure.Options;

public class AzureOptions
{
	public bool Development { get; set; } = false;
	public StorageOptions Storage { get; set; } = null!;
	public DatabaseOptions Database { get; set; } = null!;
	public ProviderSigningOptions ProviderSigning { get; set; } = null!;
}

public class StorageOptions
{
	public string Account { get; set; } = null!;
	public string Container { get; set; } = null!;
	public Uri BlobClientUrl => new($"https://{Account}.blob.core.windows.net/{Container}");
}
public class DatabaseOptions
{
	public string Connection { get; set; } = null!;
	public string Name { get; set; } = null!;
}

public class ProviderSigningOptions
{
	public string PrivateKey { get; set; } = null!;
	public string? Passphrase { get; set; }
}
