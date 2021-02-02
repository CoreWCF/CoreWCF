// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;

namespace CoreWCF.IdentityModel.Tokens
{
    public sealed class RsaSecurityKey : AsymmetricSecurityKey
    {
        private PrivateKeyStatus _privateKeyStatus = PrivateKeyStatus.AvailabilityNotDetermined;
        private readonly RSA _rsa;

        public RsaSecurityKey(RSA rsa)
        {
            _rsa = rsa ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(rsa));
        }

        public override int KeySize
        {
            get { return _rsa.KeySize; }
        }

        public override byte[] DecryptKey(string algorithm, byte[] keyData)
        {
            switch (algorithm)
            {
                case SecurityAlgorithms.RsaV15KeyWrap:
                    return EncryptedXml.DecryptKey(keyData, _rsa, false);
                case SecurityAlgorithms.RsaOaepKeyWrap:
                    return EncryptedXml.DecryptKey(keyData, _rsa, true);
                default:
                    if (IsSupportedAlgorithm(algorithm))
                    {
                        return EncryptedXml.DecryptKey(keyData, _rsa, false);
                    }

                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CryptographicException(SR.Format("c",
                 algorithm, "DecryptKey")));
            }
        }

        public override byte[] EncryptKey(string algorithm, byte[] keyData)
        {
            switch (algorithm)
            {
                case SecurityAlgorithms.RsaV15KeyWrap:
                    return EncryptedXml.EncryptKey(keyData, _rsa, false);
                case SecurityAlgorithms.RsaOaepKeyWrap:
                    return EncryptedXml.EncryptKey(keyData, _rsa, true);
                default:
                    if (IsSupportedAlgorithm(algorithm))
                    {
                        return EncryptedXml.EncryptKey(keyData, _rsa, false);
                    }

                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CryptographicException(SR.Format(SR.UnsupportedAlgorithmForCryptoOperation,
                        algorithm, nameof(EncryptKey))));
            }
        }

        public override AsymmetricAlgorithm GetAsymmetricAlgorithm(string algorithm, bool requiresPrivateKey)
        {
            if (requiresPrivateKey && !HasPrivateKey())
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CryptographicException(SR.NoPrivateKeyAvailable));
            }

            return _rsa;
        }

        public override HashAlgorithm GetHashAlgorithmForSignature(string algorithm)
        {
            if (string.IsNullOrEmpty(algorithm))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(algorithm, SR.Format(SR.EmptyOrNullArgumentString, algorithm));
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

                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CryptographicException(SR.Format(SR.UnsupportedCryptoAlgorithm,
                        algorithm)));
            }

            switch (algorithm)
            {
                case SecurityAlgorithms.RsaSha1Signature:
                    return CryptoHelper.NewSha1HashAlgorithm();
                case SecurityAlgorithms.RsaSha256Signature:
                    return CryptoHelper.NewSha256HashAlgorithm();
                default:
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CryptographicException(SR.Format(SR.UnsupportedAlgorithmForCryptoOperation,
                        algorithm, "GetHashAlgorithmForSignature")));
            }
        }

        public override AsymmetricSignatureDeformatter GetSignatureDeformatter(string algorithm)
        {
            if (string.IsNullOrEmpty(algorithm))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(algorithm, SR.Format(SR.EmptyOrNullArgumentString, algorithm));
            }

            object algorithmObject = CryptoHelper.GetAlgorithmFromConfig(algorithm);
            if (algorithmObject != null)
            {
                if (algorithmObject is SignatureDescription description)
                {
                    return description.CreateDeformatter(_rsa);
                }

                try
                {
                    if (algorithmObject is AsymmetricSignatureDeformatter asymmetricSignatureDeformatter)
                    {
                        asymmetricSignatureDeformatter.SetKey(_rsa);
                        return asymmetricSignatureDeformatter;
                    }
                }
                catch (InvalidCastException e)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.AlgorithmAndKeyMisMatch, algorithm), e));
                }

                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CryptographicException(SR.Format(SR.UnsupportedAlgorithmForCryptoOperation,
                       algorithm, "GetSignatureDeformatter")));
            }

            switch (algorithm)
            {
                case SecurityAlgorithms.RsaSha1Signature:
                case SecurityAlgorithms.RsaSha256Signature:
                    return new RSAPKCS1SignatureDeformatter(_rsa);
                default:
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CryptographicException(SR.Format(SR.UnsupportedAlgorithmForCryptoOperation,
                        algorithm, "GetSignatureDeformatter")));
            }
        }

        public override AsymmetricSignatureFormatter GetSignatureFormatter(string algorithm)
        {
            if (string.IsNullOrEmpty(algorithm))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(algorithm, SR.Format(SR.EmptyOrNullArgumentString, algorithm));
            }

            object algorithmObject = CryptoHelper.GetAlgorithmFromConfig(algorithm);
            if (algorithmObject != null)
            {
                if (algorithmObject is SignatureDescription description)
                {
                    return description.CreateFormatter(_rsa);
                }

                try
                {
                    if (algorithmObject is AsymmetricSignatureFormatter asymmetricSignatureFormatter)
                    {
                        asymmetricSignatureFormatter.SetKey(_rsa);
                        return asymmetricSignatureFormatter;
                    }
                }
                catch (InvalidCastException e)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.AlgorithmAndKeyMisMatch, algorithm), e));
                }

                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CryptographicException(SR.Format(SR.UnsupportedAlgorithmForCryptoOperation,
                       algorithm, nameof(GetSignatureFormatter))));
            }

            switch (algorithm)
            {
                case SecurityAlgorithms.RsaSha1Signature:
                case SecurityAlgorithms.RsaSha256Signature:
                    // Ensure that we have an RSA algorithm object.
                    return new RSAPKCS1SignatureFormatter(_rsa);
                default:
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CryptographicException(SR.Format(SR.UnsupportedAlgorithmForCryptoOperation,
                        algorithm, "GetSignatureFormatter")));
            }
        }

        public override bool HasPrivateKey()
        {
            if (_privateKeyStatus == PrivateKeyStatus.AvailabilityNotDetermined)
            {
                if (_rsa is RSACryptoServiceProvider rsaCryptoServiceProvider)
                {
                    _privateKeyStatus = rsaCryptoServiceProvider.PublicOnly ? PrivateKeyStatus.DoesNotHavePrivateKey : PrivateKeyStatus.HasPrivateKey;
                }
                else
                {
                    try
                    {
                        byte[] hash = new byte[20];
                        _rsa.DecryptValue(hash); // imitate signing
                        _privateKeyStatus = PrivateKeyStatus.HasPrivateKey;
                    }
#pragma warning disable CA1031 // Do not catch general exception types - interpret as no private key, don't need to use exception 
                    catch (CryptographicException)
                    {
                        _privateKeyStatus = PrivateKeyStatus.DoesNotHavePrivateKey;
                    }
#pragma warning restore CA1031 // Do not catch general exception types
                }
            }
            return _privateKeyStatus == PrivateKeyStatus.HasPrivateKey;
        }

        public override bool IsSupportedAlgorithm(string algorithm)
        {
            if (string.IsNullOrEmpty(algorithm))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(algorithm, SR.Format(SR.EmptyOrNullArgumentString, algorithm));
            }
            object algorithmObject;
            try
            {
                algorithmObject = CryptoHelper.GetAlgorithmFromConfig(algorithm);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (InvalidOperationException)
            {
                algorithmObject = null;
                // We swallow the exception and continue.
            }
#pragma warning restore CA1031 // Do not catch general exception types

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
                case SecurityAlgorithms.RsaV15KeyWrap:
                case SecurityAlgorithms.RsaOaepKeyWrap:
                case SecurityAlgorithms.RsaSha1Signature:
                case SecurityAlgorithms.RsaSha256Signature:
                    return true;
                default:
                    return false;
            }
        }

        private enum PrivateKeyStatus
        {
            AvailabilityNotDetermined,
            HasPrivateKey,
            DoesNotHavePrivateKey
        }
    }
}
