// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using CoreWCF.IdentityModel.Selectors;

namespace CoreWCF.Security
{
    public class IssuedTokenServiceCredential
    {
        internal const bool DefaultAllowUntrustedRsaIssuers = false;
        internal const AudienceUriMode DefaultAudienceUriMode = AudienceUriMode.Always;
        internal const X509CertificateValidationMode DefaultCertificateValidationMode = X509CertificateValidationMode.ChainTrust;
        internal const X509RevocationMode DefaultRevocationMode = X509RevocationMode.Online;
        internal const StoreLocation DefaultTrustedStoreLocation = StoreLocation.LocalMachine;
        private readonly List<string> allowedAudienceUris;
        private AudienceUriMode audienceUriMode = DefaultAudienceUriMode;
        private readonly List<X509Certificate2> knownCertificates;
        private SamlSerializer samlSerializer;
        private X509CertificateValidationMode certificateValidationMode = DefaultCertificateValidationMode;
        private X509RevocationMode revocationMode = DefaultRevocationMode;
        private StoreLocation trustedStoreLocation = DefaultTrustedStoreLocation;
        private X509CertificateValidator customCertificateValidator = null;
        private bool allowUntrustedRsaIssuers = DefaultAllowUntrustedRsaIssuers;
        private bool isReadOnly;

        internal IssuedTokenServiceCredential()
        {
            allowedAudienceUris = new List<string>();
            knownCertificates = new List<X509Certificate2>();
        }

        internal IssuedTokenServiceCredential(IssuedTokenServiceCredential other)
        {
            audienceUriMode = other.audienceUriMode;
            allowedAudienceUris = new List<string>(other.allowedAudienceUris);
            samlSerializer = other.samlSerializer;
            knownCertificates = new List<X509Certificate2>(other.knownCertificates);
            certificateValidationMode = other.certificateValidationMode;
            customCertificateValidator = other.customCertificateValidator;
            trustedStoreLocation = other.trustedStoreLocation;
            revocationMode = other.revocationMode;
            allowUntrustedRsaIssuers = other.allowUntrustedRsaIssuers;
            isReadOnly = other.isReadOnly;
        }

        public IList<string> AllowedAudienceUris
        {
            get
            {
                if (isReadOnly)
                {
                    return allowedAudienceUris.AsReadOnly();
                }
                else
                {
                    return allowedAudienceUris;
                }
            }
        }

        public AudienceUriMode AudienceUriMode
        {
            get
            {
                return audienceUriMode;
            }
            set
            {
                ThrowIfImmutable();
                AudienceUriModeValidationHelper.Validate(audienceUriMode);
                audienceUriMode = value;
            }
        }


        public IList<X509Certificate2> KnownCertificates
        {
            get
            {
                if (isReadOnly)
                {
                    return knownCertificates.AsReadOnly();
                }
                else
                {
                    return knownCertificates;
                }
            }
        }

        public SamlSerializer SamlSerializer
        {
            get
            {
                return samlSerializer;
            }
            set
            {
                ThrowIfImmutable();
                samlSerializer = value;
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

        public bool AllowUntrustedRsaIssuers
        {
            get
            {
                return allowUntrustedRsaIssuers;
            }
            set
            {
                ThrowIfImmutable();
                allowUntrustedRsaIssuers = value;
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
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.MissingCustomCertificateValidator)));
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
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ObjectIsReadOnly)));
            }
        }
    }
}
