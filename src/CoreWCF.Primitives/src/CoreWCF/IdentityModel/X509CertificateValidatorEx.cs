// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.IdentityModel.Selectors;
using System.Security.Cryptography.X509Certificates;
using CoreWCF.Security;

namespace CoreWCF.IdentityModel
{ 
    /// <summary>
    /// This class wraps the four WCF validator types (Peer, Chain, PeerOrChain, and None).
    /// This class also resets the validation time each time a certificate is validated, to fix a .NET issue
    /// where certificates created after the validator is created will not chain.
    /// </summary>
    internal class X509CertificateValidatorEx : X509CertificateValidator
    {
        private X509CertificateValidationMode _certificateValidationMode;
        private X509ChainPolicy _chainPolicy;
        private X509CertificateValidator _validator;

        public X509CertificateValidatorEx(
            X509CertificateValidationMode certificateValidationMode,
            X509RevocationMode revocationMode,
            StoreLocation trustedStoreLocation)
        {
            _certificateValidationMode = certificateValidationMode;

            switch (_certificateValidationMode)
            {
                case X509CertificateValidationMode.None:
                    {
                        _validator = X509CertificateValidator.None;
                        break;
                    }

                case X509CertificateValidationMode.PeerTrust:
                    {
                        _validator = X509CertificateValidator.PeerTrust;
                        break;
                    }

                case X509CertificateValidationMode.ChainTrust:
                    {
                        bool useMachineContext = trustedStoreLocation == StoreLocation.LocalMachine;
                        _chainPolicy = new X509ChainPolicy();
                        _chainPolicy.RevocationMode = revocationMode;

                        _validator = X509CertificateValidator.CreateChainTrustValidator(useMachineContext, _chainPolicy);
                        break;
                    }

                case X509CertificateValidationMode.PeerOrChainTrust:
                    {
                        bool useMachineContext = trustedStoreLocation == StoreLocation.LocalMachine;
                        _chainPolicy = new X509ChainPolicy();
                        _chainPolicy.RevocationMode = revocationMode;

                        _validator = X509CertificateValidator.CreatePeerOrChainTrustValidator(useMachineContext, _chainPolicy);
                        break;
                    }

                case X509CertificateValidationMode.Custom:
                default:
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.ID4256)));
            }
        }

        public override void Validate(X509Certificate2 certificate)
        {
            if (_certificateValidationMode == X509CertificateValidationMode.ChainTrust ||
                 _certificateValidationMode == X509CertificateValidationMode.PeerOrChainTrust)
            {
                // This is needed due to a .NET issue where the validation time is not properly set, 
                // causing certificates created after the creation of the validator to fail chain trust.
                _chainPolicy.VerificationTime = DateTime.Now;
            }

            _validator.Validate(certificate);
        }
    }
}
