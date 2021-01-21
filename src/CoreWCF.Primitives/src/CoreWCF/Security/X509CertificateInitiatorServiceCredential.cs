// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Security.Cryptography.X509Certificates;

namespace CoreWCF.Security
{
    public sealed class X509CertificateInitiatorServiceCredential
    {
        internal const StoreLocation DefaultStoreLocation = StoreLocation.LocalMachine;
        internal const StoreName DefaultStoreName = StoreName.My;
        internal const X509FindType DefaultFindType = X509FindType.FindBySubjectDistinguishedName;
        private X509Certificate2 certificate;
        private X509ClientCertificateAuthentication authentication;
        private bool isReadOnly;

        internal X509CertificateInitiatorServiceCredential()
        {
            authentication = new X509ClientCertificateAuthentication();
        }

        internal X509CertificateInitiatorServiceCredential(X509CertificateInitiatorServiceCredential other)
        {
            certificate = other.certificate;
            authentication = new X509ClientCertificateAuthentication(other.authentication);
            isReadOnly = other.isReadOnly;
        }

        public X509Certificate2 Certificate
        {
            get
            {
                return certificate;
            }
            set
            {
                ThrowIfImmutable();
                certificate = value;
            }
        }

        public X509ClientCertificateAuthentication Authentication
        {
            get
            {
                return authentication;
            }
        }

        public void SetCertificate(string subjectName, StoreLocation storeLocation, StoreName storeName)
        {
            if (subjectName == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(subjectName));
            }
            SetCertificate(storeLocation, storeName, DefaultFindType, subjectName);
        }

        public void SetCertificate(StoreLocation storeLocation, StoreName storeName, X509FindType findType, object findValue)
        {
            if (findValue == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(findValue));
            }
            ThrowIfImmutable();
            certificate = SecurityUtils.GetCertificateFromStore(storeName, storeLocation, findType, findValue, null);
        }

        internal void MakeReadOnly()
        {
            isReadOnly = true;
            Authentication.MakeReadOnly();
        }

        private void ThrowIfImmutable()
        {
            if (isReadOnly)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
            }
        }
    }
}
