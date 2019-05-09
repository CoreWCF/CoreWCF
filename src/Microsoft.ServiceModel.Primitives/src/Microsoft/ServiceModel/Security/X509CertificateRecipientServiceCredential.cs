using System;
using System.Security.Cryptography.X509Certificates;

namespace Microsoft.ServiceModel.Security
{
    public sealed class X509CertificateRecipientServiceCredential
    {
        X509Certificate2 certificate;
        internal const StoreLocation DefaultStoreLocation = StoreLocation.LocalMachine;
        internal const StoreName DefaultStoreName = StoreName.My;
        internal const X509FindType DefaultFindType = X509FindType.FindBySubjectDistinguishedName;
        bool isReadOnly;

        internal X509CertificateRecipientServiceCredential()
        {
        }

        internal X509CertificateRecipientServiceCredential(X509CertificateRecipientServiceCredential other)
        {
            certificate = other.certificate;
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

        public void SetCertificate(string subjectName)
        {
            SetCertificate(subjectName, DefaultStoreLocation, DefaultStoreName);
        }

        public void SetCertificate(string subjectName, StoreLocation storeLocation, StoreName storeName)
        {
            if (subjectName == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("subjectName");
            }
            SetCertificate(storeLocation, storeName, DefaultFindType, subjectName);
        }

        public void SetCertificate(StoreLocation storeLocation, StoreName storeName, X509FindType findType, object findValue)
        {
            if (findValue == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("findValue");
            }
            ThrowIfImmutable();
            certificate = SecurityUtils.GetCertificateFromStore(storeName, storeLocation, findType, findValue, null);
        }

        internal void MakeReadOnly()
        {
            isReadOnly = true;
        }

        void ThrowIfImmutable()
        {
            if (isReadOnly)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
            }
        }
    }

}
