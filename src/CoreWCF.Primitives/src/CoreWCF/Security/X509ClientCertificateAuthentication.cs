using CoreWCF.IdentityModel.Selectors;
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace CoreWCF.Security
{
    public class X509ClientCertificateAuthentication
    {
        internal const X509CertificateValidationMode DefaultCertificateValidationMode = X509CertificateValidationMode.ChainTrust;
        internal const X509RevocationMode DefaultRevocationMode = X509RevocationMode.Online;
        internal const StoreLocation DefaultTrustedStoreLocation = StoreLocation.LocalMachine;
        internal const bool DefaultMapCertificateToWindowsAccount = false;
        static X509CertificateValidator defaultCertificateValidator;

        X509CertificateValidationMode certificateValidationMode = DefaultCertificateValidationMode;
        X509RevocationMode revocationMode = DefaultRevocationMode;
        StoreLocation trustedStoreLocation = DefaultTrustedStoreLocation;
        X509CertificateValidator customCertificateValidator = null;
        bool mapClientCertificateToWindowsAccount = DefaultMapCertificateToWindowsAccount;
        bool includeWindowsGroups = SspiSecurityTokenProvider.DefaultExtractWindowsGroupClaims;
        bool isReadOnly;

        internal X509ClientCertificateAuthentication()
        {
        }

        internal X509ClientCertificateAuthentication(X509ClientCertificateAuthentication other)
        {
            certificateValidationMode = other.certificateValidationMode;
            customCertificateValidator = other.customCertificateValidator;
            includeWindowsGroups = other.includeWindowsGroups;
            mapClientCertificateToWindowsAccount = other.mapClientCertificateToWindowsAccount;
            trustedStoreLocation = other.trustedStoreLocation;
            revocationMode = other.revocationMode;
            isReadOnly = other.isReadOnly;
        }

        internal static X509CertificateValidator DefaultCertificateValidator
        {
            get
            {
                if (defaultCertificateValidator == null)
                {
                    bool useMachineContext = DefaultTrustedStoreLocation == StoreLocation.LocalMachine;
                    X509ChainPolicy chainPolicy = new X509ChainPolicy();
                    chainPolicy.RevocationMode = DefaultRevocationMode;
                    defaultCertificateValidator = X509CertificateValidator.CreateChainTrustValidator(useMachineContext, chainPolicy);
                }
                return defaultCertificateValidator;
            }
        }

        public X509CertificateValidationMode CertificateValidationMode
        {
            get
            {
                return certificateValidationMode;
            }
            set
            {
                X509CertificateValidationModeHelper.Validate(value);
                ThrowIfImmutable();
                certificateValidationMode = value;
            }
        }

        public X509RevocationMode RevocationMode
        {
            get
            {
                return revocationMode;
            }
            set
            {
                ThrowIfImmutable();
                revocationMode = value;
            }
        }

        public StoreLocation TrustedStoreLocation
        {
            get
            {
                return trustedStoreLocation;
            }
            set
            {
                ThrowIfImmutable();
                trustedStoreLocation = value;
            }
        }

        public X509CertificateValidator CustomCertificateValidator
        {
            get
            {
                return customCertificateValidator;
            }
            set
            {
                ThrowIfImmutable();
                customCertificateValidator = value;
            }
        }

        public bool MapClientCertificateToWindowsAccount
        {
            get
            {
                return mapClientCertificateToWindowsAccount;
            }
            set
            {
                ThrowIfImmutable();
                mapClientCertificateToWindowsAccount = value;
            }
        }

        public bool IncludeWindowsGroups
        {
            get
            {
                return includeWindowsGroups;
            }
            set
            {
                ThrowIfImmutable();
                includeWindowsGroups = value;
            }
        }

        internal X509CertificateValidator GetCertificateValidator()
        {
            if (certificateValidationMode == X509CertificateValidationMode.None)
            {
                return X509CertificateValidator.None;
            }
            else if (certificateValidationMode == X509CertificateValidationMode.PeerTrust)
            {
                return X509CertificateValidator.PeerTrust;
            }
            else if (certificateValidationMode == X509CertificateValidationMode.Custom)
            {
                if (customCertificateValidator == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.MissingCustomCertificateValidator));
                }
                return customCertificateValidator;
            }
            else
            {
                bool useMachineContext = trustedStoreLocation == StoreLocation.LocalMachine;
                X509ChainPolicy chainPolicy = new X509ChainPolicy();
                chainPolicy.RevocationMode = revocationMode;
                if (certificateValidationMode == X509CertificateValidationMode.ChainTrust)
                {
                    return X509CertificateValidator.CreateChainTrustValidator(useMachineContext, chainPolicy);
                }
                else
                {
                    return X509CertificateValidator.CreatePeerOrChainTrustValidator(useMachineContext, chainPolicy);
                }
            }
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
