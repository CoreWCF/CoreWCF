// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Security.Tokens
{
    internal static class ServiceModelSecurityTokenTypes
    {
        private const string Namespace = "http://schemas.microsoft.com/ws/2006/05/servicemodel/tokens";
        private const string spnego = Namespace + "/Spnego";
        private const string mutualSslnego = Namespace + "/MutualSslnego";
        private const string anonymousSslnego = Namespace + "/AnonymousSslnego";
        private const string securityContext = Namespace + "/SecurityContextToken";
        private const string secureConversation = Namespace + "/SecureConversation";
        private const string sspiCredential = Namespace + "/SspiCredential";

        static public string Spnego { get { return spnego; } }
        static public string MutualSslnego { get { return mutualSslnego; } }
        static public string AnonymousSslnego { get { return anonymousSslnego; } }
        static public string SecurityContext { get { return securityContext; } }
        static public string SecureConversation { get { return secureConversation; } }
        static public string SspiCredential { get { return sspiCredential; } }
    }
}
