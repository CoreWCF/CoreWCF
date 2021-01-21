// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Security.Cryptography.X509Certificates;
using CoreWCF.IdentityModel.Selectors;

namespace CoreWCF.Security
{
    public class X509ClientCertificateAuthentication
    {
        internal const X509CertificateValidationMode DefaultCertificateValidationMode = X509CertificateValidationMode.ChainTrust;
        internal const X509RevocationMode DefaultRevocationMode = X509RevocationMode.Online;
        internal const StoreLocation DefaultTrustedStoreLocation = StoreLocation.LocalMachine;
        internal const bool DefaultMapCertificateToWindowsAccount = false;
        private static X509CertificateValidator defaultCertificateValidator;
        private X509CertificateValidationMode certificateValidationMode = DefaultCertificateValidationMode;
        private X509RevocationMode revocationMode = DefaultRevocationMode;
        private StoreLocation trustedStoreLocation = DefaultTrustedStoreLocation;
        private X509CertificateValidator customCertificateValidator = null;
        private bool mapClientCertificateToWindowsAccount = DefaultMapCertificateToWindowsAccount;
        private bool includeWindowsGroups = SspiSecurityTokenProvider.DefaultExtractWindowsGroupClaims;
        private bool isReadOnly;

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

        private void ThrowIfImmutable()
        {
            if (isReadOnly)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
            }
        }

    }
}
