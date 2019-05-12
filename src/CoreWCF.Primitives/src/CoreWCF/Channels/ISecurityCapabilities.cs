using System.Net.Security;

namespace CoreWCF.Channels
{
    public interface ISecurityCapabilities
    {
        ProtectionLevel SupportedRequestProtectionLevel { get; }
        ProtectionLevel SupportedResponseProtectionLevel { get; }
        bool SupportsClientAuthentication { get; }
        bool SupportsClientWindowsIdentity { get; }
        bool SupportsServerAuthentication { get; }
    }
}
