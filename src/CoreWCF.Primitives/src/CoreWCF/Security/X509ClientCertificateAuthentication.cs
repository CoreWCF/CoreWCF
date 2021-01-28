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
        private static X509CertificateValidator s_defaultCertificateValidator;
        private X509CertificateValidationMode _certificateValidationMode = DefaultCertificateValidationMode;
        private X509RevocationMode _revocationMode = DefaultRevocationMode;
        private StoreLocation _trustedStoreLocation = DefaultTrustedStoreLocation;
        private X509CertificateValidator _customCertificateValidator = null;
        private bool _mapClientCertificateToWindowsAccount = DefaultMapCertificateToWindowsAccount;
        private bool _includeWindowsGroups = SspiSecurityTokenProvider.DefaultExtractWindowsGroupClaims;
        private bool _isReadOnly;

        internal X509ClientCertificateAuthentication()
        {
        }

        internal X509ClientCertificateAuthentication(X509ClientCertificateAuthentication other)
        {
            _certificateValidationMode = other._certificateValidationMode;
            _customCertificateValidator = other._customCertificateValidator;
            _includeWindowsGroups = other._includeWindowsGroups;
            _mapClientCertificateToWindowsAccount = other._mapClientCertificateToWindowsAccount;
            _trustedStoreLocation = other._trustedStoreLocation;
            _revocationMode = other._revocationMode;
            _isReadOnly = other._isReadOnly;
        }

        internal static X509CertificateValidator DefaultCertificateValidator
        {
            get
            {
                if (s_defaultCertificateValidator == null)
                {
                    bool useMachineContext = DefaultTrustedStoreLocation == StoreLocation.LocalMachine;
                    X509ChainPolicy chainPolicy = new X509ChainPolicy();
                    chainPolicy.RevocationMode = DefaultRevocationMode;
                    s_defaultCertificateValidator = X509CertificateValidator.CreateChainTrustValidator(useMachineContext, chainPolicy);
                }
                return s_defaultCertificateValidator;
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

        public bool MapClientCertificateToWindowsAccount
        {
            get
            {
                return _mapClientCertificateToWindowsAccount;
            }
            set
            {
                ThrowIfImmutable();
                _mapClientCertificateToWindowsAccount = value;
            }
        }

        public bool IncludeWindowsGroups
        {
            get
            {
                return _includeWindowsGroups;
            }
            set
            {
                ThrowIfImmutable();
                _includeWindowsGroups = value;
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
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.MissingCustomCertificateValidator));
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
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ObjectIsReadOnly));
            }
        }
    }
}
