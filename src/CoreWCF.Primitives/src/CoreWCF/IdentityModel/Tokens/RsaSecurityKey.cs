// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;

namespace CoreWCF.IdentityModel.Tokens
{
    public sealed class RsaSecurityKey : AsymmetricSecurityKey
    {
        private PrivateKeyStatus privateKeyStatus = PrivateKeyStatus.AvailabilityNotDetermined;
        private readonly RSA rsa;

        public RsaSecurityKey(RSA rsa)
        {
            if (rsa == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(rsa));
            }

            this.rsa = rsa;
        }

        public override int KeySize
        {
            get { return rsa.KeySize; }
        }

        public override byte[] DecryptKey(string algorithm, byte[] keyData)
        {
            switch (algorithm)
            {
                case SecurityAlgorithms.RsaV15KeyWrap:
                    return EncryptedXml.DecryptKey(keyData, rsa, false);
                case SecurityAlgorithms.RsaOaepKeyWrap:
                    return EncryptedXml.DecryptKey(keyData, rsa, true);
                default:
                    if (IsSupportedAlgorithm(algorithm))
                    {
                        return EncryptedXml.DecryptKey(keyData, rsa, false);
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
                    return EncryptedXml.EncryptKey(keyData, rsa, false);
                case SecurityAlgorithms.RsaOaepKeyWrap:
                    return EncryptedXml.EncryptKey(keyData, rsa, true);
                default:
                    if (IsSupportedAlgorithm(algorithm))
                    {
                        return EncryptedXml.EncryptKey(keyData, rsa, false);
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

            return rsa;
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
                SignatureDescription description = algorithmObject as SignatureDescription;
                if (description != null)
                {
                    return description.CreateDigest();
                }

                HashAlgorithm hashAlgorithm = algorithmObject as HashAlgorithm;
                if (hashAlgorithm != null)
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
                SignatureDescription description = algorithmObject as SignatureDescription;
                if (description != null)
                {
                    return description.CreateDeformatter(rsa);
                }

                try
                {
                    AsymmetricSignatureDeformatter asymmetricSignatureDeformatter = algorithmObject as AsymmetricSignatureDeformatter;
                    if (asymmetricSignatureDeformatter != null)
                    {
                        asymmetricSignatureDeformatter.SetKey(rsa);
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
                    return new RSAPKCS1SignatureDeformatter(rsa);
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
                SignatureDescription description = algorithmObject as SignatureDescription;
                if (description != null)
                {
                    return description.CreateFormatter(rsa);
                }

                try
                {
                    AsymmetricSignatureFormatter asymmetricSignatureFormatter = algorithmObject as AsymmetricSignatureFormatter;

                    if (asymmetricSignatureFormatter != null)
                    {
                        asymmetricSignatureFormatter.SetKey(rsa);
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
                    return new RSAPKCS1SignatureFormatter(rsa);
                default:
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CryptographicException(SR.Format(SR.UnsupportedAlgorithmForCryptoOperation,
                        algorithm, "GetSignatureFormatter")));
            }
        }

        public override bool HasPrivateKey()
        {
            if (privateKeyStatus == PrivateKeyStatus.AvailabilityNotDetermined)
            {
                RSACryptoServiceProvider rsaCryptoServiceProvider = rsa as RSACryptoServiceProvider;
                if (rsaCryptoServiceProvider != null)
                {
                    privateKeyStatus = rsaCryptoServiceProvider.PublicOnly ? PrivateKeyStatus.DoesNotHavePrivateKey : PrivateKeyStatus.HasPrivateKey;
                }
                else
                {
                    try
                    {
                        byte[] hash = new byte[20];
                        rsa.DecryptValue(hash); // imitate signing
                        privateKeyStatus = PrivateKeyStatus.HasPrivateKey;
                    }
                    catch (CryptographicException)
                    {
                        privateKeyStatus = PrivateKeyStatus.DoesNotHavePrivateKey;
                    }
                }
            }
            return privateKeyStatus == PrivateKeyStatus.HasPrivateKey;
        }

        public override bool IsSupportedAlgorithm(string algorithm)
        {
            if (string.IsNullOrEmpty(algorithm))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(algorithm, SR.Format(SR.EmptyOrNullArgumentString, algorithm));
            }
            object algorithmObject = null;
            try
            {
                algorithmObject = CryptoHelper.GetAlgorithmFromConfig(algorithm);
            }
            catch (InvalidOperationException)
            {
                algorithmObject = null;
            }

            if (algorithmObject != null)
            {
                SignatureDescription signatureDescription = algorithmObject as SignatureDescription;
                if (signatureDescription != null)
                {
                    return true;
                }

                AsymmetricAlgorithm asymmetricAlgorithm = algorithmObject as AsymmetricAlgorithm;
                if (asymmetricAlgorithm != null)
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
