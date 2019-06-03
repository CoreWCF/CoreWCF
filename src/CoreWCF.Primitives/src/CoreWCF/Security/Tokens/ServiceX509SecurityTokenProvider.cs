using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace CoreWCF.Security.Tokens
{
    internal class ServiceX509SecurityTokenProvider : X509SecurityTokenProvider
    {
        public ServiceX509SecurityTokenProvider(X509Certificate2 certificate)
            : base(certificate)
        {
        }

        public ServiceX509SecurityTokenProvider(StoreLocation storeLocation, StoreName storeName, X509FindType findType, object findValue)
            : base(storeLocation, storeName, findType, findValue)
        {
        }

        protected override SecurityToken GetTokenCore(TimeSpan timeout)
        {
            return new X509SecurityToken(Certificate, false, false);
        }
    }
}
