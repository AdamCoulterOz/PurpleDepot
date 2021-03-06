using System.Text.Json.Serialization;

namespace PurpleDepot.Core.Interface.Model.Provider;

public class SigningKeys
{
	[JsonPropertyName("gpg_public_keys")]
	public List<GpgPublicKey> GpgPublicKeys { get; set; }

	[JsonConstructor]
	public SigningKeys(List<GpgPublicKey> gpg_public_keys)
		=> GpgPublicKeys = gpg_public_keys;

#nullable disable
	protected SigningKeys() { }
}
