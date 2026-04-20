using System.Text;
using PurpleDepot.Core.Controller;
using PurpleDepot.Core.Interface.Model.Provider;
using PurpleDepot.Providers.Azure.Options;
using Microsoft.Extensions.Options;
using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.OpenPgp;

namespace PurpleDepot.Providers.Azure;

public class OpenPgpProviderPackageSigner : IProviderPackageSigner
{
	private readonly PgpSecretKey _secretKey;
	private readonly char[] _passphrase;
	private readonly SigningKeys _signingKeys;

	public OpenPgpProviderPackageSigner(IOptions<ProviderSigningOptions> options)
	{
		var signingOptions = options.Value;
		if (string.IsNullOrWhiteSpace(signingOptions.PrivateKey))
			throw new InvalidOperationException("Missing required provider signing private key configuration.");

		_secretKey = LoadSigningKey(signingOptions.PrivateKey);
		_passphrase = signingOptions.Passphrase?.ToCharArray() ?? Array.Empty<char>();
		_signingKeys = new SigningKeys(new List<GpgPublicKey>
		{
			new(
				key_id: $"{_secretKey.PublicKey.KeyId:X16}",
				ascii_armor: ExportPublicKey(_secretKey.PublicKey))
		});
	}

	public SigningKeys GetSigningKeys() => _signingKeys;

	public Task<byte[]> SignAsync(byte[] content)
	{
		var privateKey = _secretKey.ExtractPrivateKey(_passphrase);
		var signatureGenerator = new PgpSignatureGenerator(_secretKey.PublicKey.Algorithm, HashAlgorithmTag.Sha256);
		signatureGenerator.InitSign(PgpSignature.BinaryDocument, privateKey);
		signatureGenerator.Update(content, 0, content.Length);

		using var output = new MemoryStream();
		var bcpgOutput = new BcpgOutputStream(output);
		signatureGenerator.Generate().Encode(bcpgOutput);
		return Task.FromResult(output.ToArray());
	}

	private static PgpSecretKey LoadSigningKey(string armoredPrivateKey)
	{
		using var input = PgpUtilities.GetDecoderStream(new MemoryStream(Encoding.UTF8.GetBytes(armoredPrivateKey)));
		var bundle = new PgpSecretKeyRingBundle(input);

		foreach (PgpSecretKeyRing keyRing in bundle.GetKeyRings())
		{
			foreach (PgpSecretKey key in keyRing.GetSecretKeys())
			{
				if (key.IsSigningKey)
					return key;
			}
		}

		throw new InvalidOperationException("No signing-capable OpenPGP key was found in provider signing configuration.");
	}

	private static string ExportPublicKey(PgpPublicKey publicKey)
	{
		using var output = new MemoryStream();
		using (var armored = new ArmoredOutputStream(output))
		{
			publicKey.Encode(armored);
		}

		return Encoding.UTF8.GetString(output.ToArray());
	}
}
