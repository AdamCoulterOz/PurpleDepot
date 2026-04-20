using PurpleDepot.Core.Interface.Model.Provider;

namespace PurpleDepot.Core.Controller;

public interface IProviderPackageSigner
{
	SigningKeys GetSigningKeys();
	Task<byte[]> SignAsync(byte[] content);
}
