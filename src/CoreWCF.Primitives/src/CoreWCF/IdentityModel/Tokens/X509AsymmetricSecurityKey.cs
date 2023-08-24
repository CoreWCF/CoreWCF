// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;

namespace CoreWCF.IdentityModel.Tokens
{
    public class X509AsymmetricSecurityKey : AsymmetricSecurityKey
    {
        private readonly X509Certificate2 _certificate;
        private AsymmetricAlgorithm _privateKey;
        private bool _privateKeyAvailabilityDetermined;
        private AsymmetricAlgorithm _publicKey;
        private bool _publicKeyAvailabilityDetermined;

        public X509AsymmetricSecurityKey(X509Certificate2 certificate)
        {
            _certificate = certificate ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(certificate));
        }

        public override int KeySize
        {
            get { return PublicKey.KeySize; }
        }

        private AsymmetricAlgorithm PrivateKey
        {
            get
            {
                if (!_privateKeyAvailabilityDetermined)
                {
                    lock (ThisLock)
                    {
                        _privateKey = _certificate.GetRSAPrivateKey();
                        if (_privateKey != null)
                        {
                            // ProviderType == 1 is PROV_RSA_FULL provider type that only supports SHA1.
                            // Change it to PROV_RSA_AES=24 that supports SHA2 also.
                            if (_privateKey is RSACryptoServiceProvider rsaCsp && rsaCsp.CspKeyContainerInfo.ProviderType == 1)
                            {
                                CspParameters csp = new CspParameters
                                {
                                    ProviderType = 24,
                                    KeyContainerName = rsaCsp.CspKeyContainerInfo.KeyContainerName,
                                    KeyNumber = (int)rsaCsp.CspKeyContainerInfo.KeyNumber
                                };
                                if (rsaCsp.CspKeyContainerInfo.MachineKeyStore)
                                {
                                    csp.Flags = CspProviderFlags.UseMachineKeyStore;
                                }

                                csp.Flags |= CspProviderFlags.UseExistingKey;
                                _privateKey = new RSACryptoServiceProvider(csp);
                            }
                        }
                        else
                        {
                            _privateKey = _certificate.GetECDsaPrivateKey();
                            // We don't support DSA certificates as we need DSACertificateExtensions which is in netstandard2.1 and netcore2.0.
                            // As we target netstandard2.0, we don't have access to DSACertificateExtensions. If there's demand, we could move our
                            // dependencies forward to provide support.
                        }

                        if (_certificate.HasPrivateKey && _privateKey == null)
                        {
                            DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.PrivateKeyNotSupported));
                        }

                        _privateKeyAvailabilityDetermined = true;
                    }
                }
                return _privateKey;
            }
        }

        private AsymmetricAlgorithm PublicKey
        {
            get
            {
                if (!_publicKeyAvailabilityDetermined)
                {
                    lock (ThisLock)
                    {
                        if (!_publicKeyAvailabilityDetermined)
                        {
                            _publicKey = _certificate.GetRSAPublicKey();
                            if (_publicKey == null)
                            {
                                // Need DSACertificateExtensions  to support DSA certificate which is in netstandard2.1 and netcore2.0. As we target netstandard2.0, we don't
                                // have access to DSACertificateExtensions
                                DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.PublicKeyNotSupported));
                            }

                            _publicKeyAvailabilityDetermined = true;
                        }
                    }
                }
                return _publicKey;
            }
        }

        private object ThisLock { get; } = new object();

        public override byte[] DecryptKey(string algorithm, byte[] keyData)
        {
            throw new PlatformNotSupportedException();
        }

        public override byte[] EncryptKey(string algorithm, byte[] keyData)
        {
            throw new PlatformNotSupportedException();
        }

        public override AsymmetricAlgorithm GetAsymmetricAlgorithm(string algorithm, bool privateKey)
        {
            if (privateKey)
            {
                if (PrivateKey == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.NoPrivateKeyAvailable));
                }

                if (string.IsNullOrEmpty(algorithm))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(algorithm, SR.Format(SR.EmptyOrNullArgumentString, nameof(algorithm)));
                }

                switch (algorithm)
                {
                    case SignedXml.XmlDsigDSAUrl:
                        if ((PrivateKey as DSA) != null)
                        {
                            return (PrivateKey as DSA);
                        }
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.AlgorithmAndPrivateKeyMisMatch));

                    case SignedXml.XmlDsigRSASHA1Url:
                    case SecurityAlgorithms.RsaSha256Signature:
                    case EncryptedXml.XmlEncRSA15Url:
                    case EncryptedXml.XmlEncRSAOAEPUrl:
                        if ((PrivateKey as RSA) != null)
                        {
                            return (PrivateKey as RSA);
                        }
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.AlgorithmAndPrivateKeyMisMatch));
                    default:
                        if (IsSupportedAlgorithm(algorithm))
                        {
                            return PrivateKey;
                        }
                        else
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.UnsupportedCryptoAlgorithm, algorithm)));
                        }
                }
            }
            else
            {
                switch (algorithm)
                {
                    case SignedXml.XmlDsigDSAUrl:
                        if (PublicKey is DSA dsaPrivateKey)
                        {
                            return dsaPrivateKey;
                        }
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException("AlgorithmAndPublicKeyMisMatch"));
                    case SignedXml.XmlDsigRSASHA1Url:
                    case SecurityAlgorithms.RsaSha256Signature:
                    case EncryptedXml.XmlEncRSA15Url:
                    case EncryptedXml.XmlEncRSAOAEPUrl:
                        if ((PublicKey as RSA) != null)
                        {
                            return (PublicKey as RSA);
                        }
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException("AlgorithmAndPublicKeyMisMatch"));
                    default:

                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.UnsupportedCryptoAlgorithm, algorithm)));
                }
            }
        }

        public override HashAlgorithm GetHashAlgorithmForSignature(string algorithm)
        {
            if (string.IsNullOrEmpty(algorithm))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(algorithm, SR.Format(SR.EmptyOrNullArgumentString, nameof(algorithm)));
            }

            object algorithmObject = CryptoHelper.GetAlgorithmFromConfig(algorithm);

            if (algorithmObject != null)
            {
                if (algorithmObject is SignatureDescription description)
                {
                    return description.CreateDigest();
                }

                if (algorithmObject is HashAlgorithm hashAlgorithm)
                {
                    return hashAlgorithm;
                }

                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CryptographicException(SR.Format(SR.UnsupportedAlgorithmForCryptoOperation,
                        algorithm, "CreateDigest")));
            }

            switch (algorithm)
            {
                case SignedXml.XmlDsigDSAUrl:
                case SignedXml.XmlDsigRSASHA1Url:
                    return CryptoHelper.NewSha1HashAlgorithm();
                case SecurityAlgorithms.RsaSha256Signature:
                    return CryptoHelper.NewSha256HashAlgorithm();
                default:
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.UnsupportedCryptoAlgorithm, algorithm)));
            }
        }

        public override AsymmetricSignatureDeformatter GetSignatureDeformatter(string algorithm)
        {
            // We support one of the two algoritms, but not both.
            //     XmlDsigDSAUrl = "http://www.w3.org/2000/09/xmldsig#dsa-sha1";
            //     XmlDsigRSASHA1Url = "http://www.w3.org/2000/09/xmldsig#rsa-sha1";

            if (string.IsNullOrEmpty(algorithm))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(algorithm, SR.Format(SR.EmptyOrNullArgumentString, nameof(algorithm)));
            }

            object algorithmObject = CryptoHelper.GetAlgorithmFromConfig(algorithm);
            if (algorithmObject != null)
            {
                if (algorithmObject is SignatureDescription description)
                {
                    return description.CreateDeformatter(PublicKey);
                }

                try
                {
                    if (algorithmObject is AsymmetricSignatureDeformatter asymmetricSignatureDeformatter)
                    {
                        asymmetricSignatureDeformatter.SetKey(PublicKey);
                        return asymmetricSignatureDeformatter;
                    }
                }
                catch (InvalidCastException e)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.AlgorithmAndPublicKeyMisMatch, e));
                }

                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CryptographicException(SR.Format(SR.UnsupportedAlgorithmForCryptoOperation,
                       algorithm, nameof(GetSignatureDeformatter))));
            }

            switch (algorithm)
            {
                case SignedXml.XmlDsigDSAUrl:

                    // Ensure that we have a DSA algorithm object.
                    DSA dsa = (PublicKey as DSA);
                    if (dsa == null)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException("PublicKeyNotDSA"));
                    }

                    return new DSASignatureDeformatter(dsa);

                case SignedXml.XmlDsigRSASHA1Url:
                case SecurityAlgorithms.RsaSha256Signature:
                    // Ensure that we have an RSA algorithm object.
                    RSA rsa = (PublicKey as RSA);
                    if (rsa == null)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.PublicKeyNotRSA));
                    }

                    return new RSAPKCS1SignatureDeformatter(rsa);

                default:
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.UnsupportedCryptoAlgorithm, algorithm)));
            }
        }

        public override AsymmetricSignatureFormatter GetSignatureFormatter(string algorithm)
        {
            // One can sign only if the private key is present.
            if (PrivateKey == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.NoPrivateKeyAvailable));
            }

            if (string.IsNullOrEmpty(algorithm))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(algorithm, SR.Format(SR.EmptyOrNullArgumentString, nameof(algorithm)));
            }

            // We support:
            //     XmlDsigDSAUrl = "http://www.w3.org/2000/09/xmldsig#dsa-sha1";
            //     XmlDsigRSASHA1Url = "http://www.w3.org/2000/09/xmldsig#rsa-sha1";
            //     RsaSha256Signature = "http://www.w3.org/2001/04/xmldsig-more#rsa-sha256";
            AsymmetricAlgorithm privateKey = PrivateKey;

            object algorithmObject = CryptoHelper.GetAlgorithmFromConfig(algorithm);
            if (algorithmObject != null)
            {
                if (algorithmObject is SignatureDescription description)
                {
                    return description.CreateFormatter(privateKey);
                }

                try
                {
                    if (algorithmObject is AsymmetricSignatureFormatter asymmetricSignatureFormatter)
                    {
                        asymmetricSignatureFormatter.SetKey(privateKey);
                        return asymmetricSignatureFormatter;
                    }
                }
                catch (InvalidCastException e)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.AlgorithmAndPrivateKeyMisMatch, e));
                }

                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CryptographicException(SR.Format(SR.UnsupportedAlgorithmForCryptoOperation,
                       algorithm, nameof(GetSignatureFormatter))));
            }

            switch (algorithm)
            {
                case SignedXml.XmlDsigDSAUrl:

                    // Ensure that we have a DSA algorithm object.
                    DSA dsa = (PrivateKey as DSA);
                    if (dsa == null)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.PrivateKeyNotDSA));
                    }
                    return new DSASignatureFormatter(dsa);
                case SignedXml.XmlDsigRSASHA1Url:
                    // Ensure that we have an RSA algorithm object.
                    RSA rsa = (PrivateKey as RSA);
                    if (rsa == null)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.PrivateKeyNotRSA));
                    }

                    return new RSAPKCS1SignatureFormatter(rsa);

                case SecurityAlgorithms.RsaSha256Signature:
                    // Ensure that we have an RSA algorithm object.
                    RSA rsaSha256 = (privateKey as RSA);
                    if (rsaSha256 == null)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.PrivateKeyNotRSA));
                    }

                    return new RSAPKCS1SignatureFormatter(rsaSha256);

                default:
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.UnsupportedCryptoAlgorithm, algorithm)));
            }
        }

        public override bool HasPrivateKey()
        {
            return (PrivateKey != null);
        }

        public override bool IsSupportedAlgorithm(string algorithm)
        {
            if (string.IsNullOrEmpty(algorithm))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(algorithm, SR.Format(SR.EmptyOrNullArgumentString, nameof(algorithm)));
            }

            object algorithmObject = null;
            try
            {
                algorithmObject = CryptoHelper.GetAlgorithmFromConfig(algorithm);
            }
            catch (InvalidOperationException)
            {
                algorithm = null;
            }

            if (algorithmObject != null)
            {
                if (algorithmObject is SignatureDescription signatureDescription)
                {
                    return true;
                }

                if (algorithmObject is AsymmetricAlgorithm asymmetricAlgorithm)
                {
                    return true;
                }

                return false;
            }

            switch (algorithm)
            {
                case SignedXml.XmlDsigDSAUrl:
                    return (PublicKey is DSA);

                case SignedXml.XmlDsigRSASHA1Url:
                case SecurityAlgorithms.RsaSha256Signature:
                case EncryptedXml.XmlEncRSA15Url:
                case EncryptedXml.XmlEncRSAOAEPUrl:
                    return (PublicKey is RSA);
                default:
                    return false;
            }
        }
    }
}
