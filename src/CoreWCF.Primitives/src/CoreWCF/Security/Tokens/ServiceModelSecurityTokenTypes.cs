// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Security.Tokens
{
    public static class ServiceModelSecurityTokenTypes
    {
        private const string Namespace = "http://schemas.microsoft.com/ws/2006/05/servicemodel/tokens";
        private const string spnego = Namespace + "/Spnego";
        private const string mutualSslnego = Namespace + "/MutualSslnego";
        private const string anonymousSslnego = Namespace + "/AnonymousSslnego";
        private const string securityContext = Namespace + "/SecurityContextToken";
        private const string secureConversation = Namespace + "/SecureConversation";
        private const string sspiCredential = Namespace + "/SspiCredential";

        public static string Spnego { get { return spnego; } }
        public static string MutualSslnego { get { return mutualSslnego; } }
        public static string AnonymousSslnego { get { return anonymousSslnego; } }
        public static string SecurityContext { get { return securityContext; } }
        public static string SecureConversation { get { return secureConversation; } }
        public static string SspiCredential { get { return sspiCredential; } }
    }
}
