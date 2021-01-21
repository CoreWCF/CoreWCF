// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Security.Cryptography.X509Certificates;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Security;

namespace CoreWCF.IdentityModel.Selectors
{
    internal class X509SecurityTokenProvider : SecurityTokenProvider, IDisposable
    {
        private readonly X509Certificate2 certificate;

        public X509SecurityTokenProvider(X509Certificate2 certificate)
        {
            if (certificate == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(certificate));
            }

            this.certificate = new X509Certificate2(certificate);
        }

        public X509SecurityTokenProvider(StoreLocation storeLocation, StoreName storeName, X509FindType findType, object findValue)
        {
            if (findValue == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(findValue));
            }

            X509Store store = new X509Store(storeName, storeLocation);
            X509Certificate2Collection certificates = null;
            try
            {
                store.Open(OpenFlags.ReadOnly);
                certificates = store.Certificates.Find(findType, findValue, false);
                if (certificates.Count < 1)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityTokenException(SR.Format(SR.CannotFindCert, storeName, storeLocation, findType, findValue)));
                }
                if (certificates.Count > 1)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityTokenException(SR.Format(SR.FoundMultipleCerts, storeName, storeLocation, findType, findValue)));
                }

                certificate = new X509Certificate2(certificates[0]);
            }
            finally
            {
                SecurityUtils.ResetAllCertificates(certificates);
                store.Close();
            }
        }

        public X509Certificate2 Certificate
        {
            get { return certificate; }
        }

        protected override SecurityToken GetTokenCore(TimeSpan timeout)
        {
            return new X509SecurityToken(certificate);
        }

        public void Dispose()
        {
            SecurityUtils.ResetCertificate(certificate);
        }
    }

}
