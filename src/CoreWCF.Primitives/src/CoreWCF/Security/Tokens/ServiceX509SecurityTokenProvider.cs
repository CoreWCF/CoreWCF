// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Security.Cryptography.X509Certificates;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;

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
