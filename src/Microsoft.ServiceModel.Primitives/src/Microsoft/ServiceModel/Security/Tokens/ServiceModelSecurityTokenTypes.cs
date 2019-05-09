using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.ServiceModel.Security.Tokens
{
    internal static class ServiceModelSecurityTokenTypes
    {
        const string Namespace = "http://schemas.microsoft.com/ws/2006/05/servicemodel/tokens";
        const string spnego = Namespace + "/Spnego";
        const string mutualSslnego = Namespace + "/MutualSslnego";
        const string anonymousSslnego = Namespace + "/AnonymousSslnego";
        const string securityContext = Namespace + "/SecurityContextToken";
        const string secureConversation = Namespace + "/SecureConversation";
        const string sspiCredential = Namespace + "/SspiCredential";

        static public string Spnego { get { return spnego; } }
        static public string MutualSslnego { get { return mutualSslnego; } }
        static public string AnonymousSslnego { get { return anonymousSslnego; } }
        static public string SecurityContext { get { return securityContext; } }
        static public string SecureConversation { get { return secureConversation; } }
        static public string SspiCredential { get { return sspiCredential; } }
    }
}
