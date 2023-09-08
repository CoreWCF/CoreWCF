using System;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    internal static class UnixDomainSocketUri
    {
        public static string UriSchemeUDS = "net.uds";
        public static void Validate(Uri uri)
        {
            if (uri.Scheme != UriSchemeUDS)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(uri), SR.UDSUriSchemeWrong);
        }
    }
}

