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
        private X509Certificate2 _certificate;
        private bool _isReadOnly;

        internal X509CertificateInitiatorServiceCredential()
        {
            Authentication = new X509ClientCertificateAuthentication();
        }

        internal X509CertificateInitiatorServiceCredential(X509CertificateInitiatorServiceCredential other)
        {
            _certificate = other._certificate;
            Authentication = new X509ClientCertificateAuthentication(other.Authentication);
            _isReadOnly = other._isReadOnly;
        }

        public X509Certificate2 Certificate
        {
            get
            {
                return _certificate;
            }
            set
            {
                ThrowIfImmutable();
                _certificate = value;
            }
        }

        public X509ClientCertificateAuthentication Authentication { get; }

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
            _certificate = SecurityUtils.GetCertificateFromStore(storeName, storeLocation, findType, findValue, null);
        }

        internal void MakeReadOnly()
        {
            _isReadOnly = true;
            Authentication.MakeReadOnly();
        }

        private void ThrowIfImmutable()
        {
            if (_isReadOnly)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
            }
        }
    }
}
