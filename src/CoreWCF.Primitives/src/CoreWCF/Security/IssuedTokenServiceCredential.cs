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
        private readonly List<string> _allowedAudienceUris;
        private AudienceUriMode _audienceUriMode = DefaultAudienceUriMode;
        private readonly List<X509Certificate2> _knownCertificates;
        private SamlSerializer _samlSerializer;
        private X509CertificateValidationMode _certificateValidationMode = DefaultCertificateValidationMode;
        private X509RevocationMode _revocationMode = DefaultRevocationMode;
        private StoreLocation _trustedStoreLocation = DefaultTrustedStoreLocation;
        private X509CertificateValidator _customCertificateValidator = null;
        private bool _allowUntrustedRsaIssuers = DefaultAllowUntrustedRsaIssuers;
        private bool _isReadOnly;

        internal IssuedTokenServiceCredential()
        {
            _allowedAudienceUris = new List<string>();
            _knownCertificates = new List<X509Certificate2>();
        }

        internal IssuedTokenServiceCredential(IssuedTokenServiceCredential other)
        {
            _audienceUriMode = other._audienceUriMode;
            _allowedAudienceUris = new List<string>(other._allowedAudienceUris);
            _samlSerializer = other._samlSerializer;
            _knownCertificates = new List<X509Certificate2>(other._knownCertificates);
            _certificateValidationMode = other._certificateValidationMode;
            _customCertificateValidator = other._customCertificateValidator;
            _trustedStoreLocation = other._trustedStoreLocation;
            _revocationMode = other._revocationMode;
            _allowUntrustedRsaIssuers = other._allowUntrustedRsaIssuers;
            _isReadOnly = other._isReadOnly;
        }

        public IList<string> AllowedAudienceUris
        {
            get
            {
                if (_isReadOnly)
                {
                    return _allowedAudienceUris.AsReadOnly();
                }
                else
                {
                    return _allowedAudienceUris;
                }
            }
        }

        public AudienceUriMode AudienceUriMode
        {
            get
            {
                return _audienceUriMode;
            }
            set
            {
                ThrowIfImmutable();
                AudienceUriModeValidationHelper.Validate(_audienceUriMode);
                _audienceUriMode = value;
            }
        }


        public IList<X509Certificate2> KnownCertificates
        {
            get
            {
                if (_isReadOnly)
                {
                    return _knownCertificates.AsReadOnly();
                }
                else
                {
                    return _knownCertificates;
                }
            }
        }

        public SamlSerializer SamlSerializer
        {
            get
            {
                return _samlSerializer;
            }
            set
            {
                ThrowIfImmutable();
                _samlSerializer = value;
            }
        }

        public X509CertificateValidationMode CertificateValidationMode
        {
            get
            {
                return _certificateValidationMode;
            }
            set
            {
                X509CertificateValidationModeHelper.Validate(value);
                ThrowIfImmutable();
                _certificateValidationMode = value;
            }
        }

        public X509RevocationMode RevocationMode
        {
            get
            {
                return _revocationMode;
            }
            set
            {
                ThrowIfImmutable();
                _revocationMode = value;
            }
        }

        public StoreLocation TrustedStoreLocation
        {
            get
            {
                return _trustedStoreLocation;
            }
            set
            {
                ThrowIfImmutable();
                _trustedStoreLocation = value;
            }
        }

        public X509CertificateValidator CustomCertificateValidator
        {
            get
            {
                return _customCertificateValidator;
            }
            set
            {
                ThrowIfImmutable();
                _customCertificateValidator = value;
            }
        }

        public bool AllowUntrustedRsaIssuers
        {
            get
            {
                return _allowUntrustedRsaIssuers;
            }
            set
            {
                ThrowIfImmutable();
                _allowUntrustedRsaIssuers = value;
            }
        }

        internal X509CertificateValidator GetCertificateValidator()
        {
            if (_certificateValidationMode == X509CertificateValidationMode.None)
            {
                return X509CertificateValidator.None;
            }
            else if (_certificateValidationMode == X509CertificateValidationMode.PeerTrust)
            {
                return X509CertificateValidator.PeerTrust;
            }
            else if (_certificateValidationMode == X509CertificateValidationMode.Custom)
            {
                if (_customCertificateValidator == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.MissingCustomCertificateValidator)));
                }
                return _customCertificateValidator;
            }
            else
            {
                bool useMachineContext = _trustedStoreLocation == StoreLocation.LocalMachine;
                X509ChainPolicy chainPolicy = new X509ChainPolicy();
                chainPolicy.RevocationMode = _revocationMode;
                if (_certificateValidationMode == X509CertificateValidationMode.ChainTrust)
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
            _isReadOnly = true;
        }

        private void ThrowIfImmutable()
        {
            if (_isReadOnly)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ObjectIsReadOnly)));
            }
        }
    }
}
