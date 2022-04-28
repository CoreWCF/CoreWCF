// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using CoreWCF.IdentityModel.Tokens;
using Linq = System.Linq;

namespace CoreWCF.IdentityModel
{
    internal static class CryptoHelper
    {
        private static RandomNumberGenerator s_random;
        private const string SHAString = "SHA";
        private const string SHA1String = "SHA1";
        private const string SHA256String = "SHA256";
        private const string SystemSecurityCryptographySha1String = "System.Security.Cryptography.SHA1";
        private static byte[] s_emptyBuffer;

        private static readonly Dictionary<string, Func<object>> s_algorithmDelegateDictionary = new Dictionary<string, Func<object>>();
        private static readonly object s_algorithmDictionaryLock = new object();

        internal static byte[] EmptyBuffer
        {
            get
            {
                if (s_emptyBuffer == null)
                {
                    byte[] tmp = Array.Empty<byte>();
                    s_emptyBuffer = tmp;
                }
                return s_emptyBuffer;
            }
        }

        public static byte[] GenerateSymmetricKey(int keySizeInBits)
        {
            int keySizeInBytes = ValidateKeySizeInBytes(keySizeInBits);
            byte[] key = new byte[keySizeInBytes];
            CryptoHelper.GenerateRandomBytes(key);
            return key;
        }

        private static int ValidateKeySizeInBytes(int keySizeInBits)
        {
            int keySizeInBytes = keySizeInBits / 8;

            if (keySizeInBits <= 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(keySizeInBits), SR.Format(SR.ID6033, keySizeInBits)));
            }
            else if (keySizeInBytes * 8 != keySizeInBits)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.Format(SR.ID6002, keySizeInBits), nameof(keySizeInBits)));
            }

            return keySizeInBytes;
        }

        internal static void ValidateBufferBounds(Array buffer, int offset, int count)
        {
            if (buffer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(buffer)));
            }
            if (count < 0 || count > buffer.Length)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(count), SR.Format(SR.ValueMustBeInRange, 0, buffer.Length)));
            }
            if (offset < 0 || offset > buffer.Length - count)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(offset), SR.Format(SR.ValueMustBeInRange, 0, buffer.Length - count)));
            }
        }

        public static bool FixedTimeEquals(byte[] a, byte[] b)
        {
            if (a == null && b == null)
            {
                return true;
            }
            else if (a == null || b == null)
            {
                return false;
            }
            else if (a.Length != b.Length)
            {
                return false;
            }

            int result = 0;
            int length = a.Length;

            for (int i = 0; i < length; i++)
            {
                result |= a[i] ^ b[i];
            }

            return result == 0;
        }

        internal static byte[] UnwrapKey(byte[] wrappingKey, byte[] wrappedKey, string algorithm)
        {
            throw new PlatformNotSupportedException();
        }

        internal static byte[] WrapKey(byte[] wrappingKey, byte[] keyToBeWrapped, string algorithm)
        {
            throw new PlatformNotSupportedException();
        }

        internal static byte[] GenerateDerivedKey(byte[] key, string algorithm, byte[] label, byte[] nonce, int derivedKeySize, int position)
        {
            throw new PlatformNotSupportedException();
        }

        internal static int GetIVSize(string algorithm)
        {
            throw new PlatformNotSupportedException();
        }

        internal static ICryptoTransform CreateDecryptor(byte[] key, byte[] iv, string algorithm)
        {
            throw new PlatformNotSupportedException();
        }

        internal static ICryptoTransform CreateEncryptor(byte[] key, byte[] iv, string algorithm)
        {
            throw new PlatformNotSupportedException();
        }

        internal static KeyedHashAlgorithm CreateKeyedHashAlgorithm(byte[] key, string algorithm)
        {
            object algorithmObject = GetAlgorithmFromConfig(algorithm);

            if (algorithmObject != null)
            {
                if (algorithmObject is KeyedHashAlgorithm keyedHashAlgorithm)
                {
                    keyedHashAlgorithm.Key = key;
                    return keyedHashAlgorithm;
                }

                throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new InvalidOperationException(SR.Format("CustomCryptoAlgorithmIsNotValidKeyedHashAlgorithm", algorithm)));
            }

            switch (algorithm)
            {
                case SecurityAlgorithms.HmacSha1Signature:
                    return new HMACSHA1(key);
                case SecurityAlgorithms.HmacSha256Signature:
                    return new HMACSHA256(key);
                default:
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new InvalidOperationException(SR.Format(SR.UnsupportedCryptoAlgorithm, algorithm)));
            }
        }

        internal static SymmetricAlgorithm GetSymmetricAlgorithm(byte[] key, string algorithm)
        {
            throw new PlatformNotSupportedException();
        }

        internal static bool IsSymmetricSupportedAlgorithm(string algorithm, int keySize)
        {
            bool found = false;
            object algorithmObject = null;

            try
            {
                algorithmObject = GetAlgorithmFromConfig(algorithm);
            }
            catch (InvalidOperationException)
            {
                // We swallow the exception and continue.
            }
            if (algorithmObject != null)
            {
                SymmetricAlgorithm symmetricAlgorithm = algorithmObject as SymmetricAlgorithm;
                KeyedHashAlgorithm keyedHashAlgorithm = algorithmObject as KeyedHashAlgorithm;

                if (symmetricAlgorithm != null || keyedHashAlgorithm != null)
                {
                    found = true;
                }
                // The reason we do not return here even when the user has provided a custom algorithm to CryptoConfig 
                // is because we need to check if the user has overwritten an existing standard URI.
            }

            switch (algorithm)
            {
                case SecurityAlgorithms.DsaSha1Signature:
                case SecurityAlgorithms.RsaSha1Signature:
                case SecurityAlgorithms.RsaSha256Signature:
                case SecurityAlgorithms.RsaOaepKeyWrap:
                case SecurityAlgorithms.RsaV15KeyWrap:
                    return false;
                case SecurityAlgorithms.HmacSha1Signature:
                case SecurityAlgorithms.HmacSha256Signature:
                case SecurityAlgorithms.Psha1KeyDerivation:
                case SecurityAlgorithms.Psha1KeyDerivationDec2005:
                    return true;
                case SecurityAlgorithms.Aes128Encryption:
                case SecurityAlgorithms.Aes128KeyWrap:
                    return keySize >= 128 && keySize <= 256;
                case SecurityAlgorithms.Aes192Encryption:
                case SecurityAlgorithms.Aes192KeyWrap:
                    return keySize >= 192 && keySize <= 256;
                case SecurityAlgorithms.Aes256Encryption:
                case SecurityAlgorithms.Aes256KeyWrap:
                    return keySize == 256;
                case SecurityAlgorithms.TripleDesEncryption:
                case SecurityAlgorithms.TripleDesKeyWrap:
                    return keySize == 128 || keySize == 192;
                default:
                    if (found)
                    {
                        return true;
                    }

                    return false;
                    // We do not expect the user to map the uri of an existing standrad algorithm with say key size 128 bit 
                    // to a custom algorithm with keySize 192 bits. If he does that, we anyways make sure that we return false.
            }
        }

        internal static void FillRandomBytes(byte[] buffer)
        {
            RandomNumberGenerator.GetBytes(buffer);
        }

        /// <summary>
        /// This generates the entropy using random number. This is usually used on the sending 
        /// side to generate the requestor's entropy.
        /// </summary>
        /// <param name="data">The array to fill with cryptographically strong random nonzero bytes.</param>
        public static void GenerateRandomBytes(byte[] data)
        {
            RandomNumberGenerator.GetNonZeroBytes(data);
        }

        /// <summary>
        /// This method generates a random byte array used as entropy with the given size. 
        /// </summary>
        /// <param name="sizeInBits"></param>
        /// <returns></returns>
        public static byte[] GenerateRandomBytes(int sizeInBits)
        {
            int sizeInBytes = sizeInBits / 8;
            if (sizeInBits <= 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(sizeInBits), SR.Format("ID6033", sizeInBits)));
            }
            else if (sizeInBytes * 8 != sizeInBits)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.Format("ID6002", sizeInBits), nameof(sizeInBits)));
            }

            byte[] data = new byte[sizeInBytes];
            GenerateRandomBytes(data);

            return data;
        }

        internal static RandomNumberGenerator RandomNumberGenerator
        {
            get
            {
                if (s_random == null)
                {
                    s_random = RandomNumberGenerator.Create();
                }
                return s_random;
            }
        }

        internal static HashAlgorithm CreateHashAlgorithm(string algorithm)
        {
            object algorithmObject = GetAlgorithmFromConfig(algorithm);

            if (algorithmObject != null)
            {
                if (algorithmObject is HashAlgorithm hashAlgorithm)
                {
                    return hashAlgorithm;
                }
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new InvalidOperationException(SR.Format(SR.CustomCryptoAlgorithmIsNotValidHashAlgorithm, algorithm)));
            }

            switch (algorithm)
            {
                case SHAString:
                case SHA1String:
                case SystemSecurityCryptographySha1String:
                case SecurityAlgorithms.Sha1Digest:
                    return SHA1.Create();
                case SHA256String:
                case SecurityAlgorithms.Sha256Digest:
                    return SHA256.Create();
                default:
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new InvalidOperationException(SR.Format(SR.UnsupportedCryptoAlgorithm, algorithm)));
            }
        }

        private static object GetDefaultAlgorithm(string algorithm)
        {
            if (string.IsNullOrEmpty(algorithm))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(algorithm)));
            }

            switch (algorithm)
            {
                //case SecurityAlgorithms.RsaSha1Signature:
                //case SecurityAlgorithms.DsaSha1Signature:
                // For these algorithms above, crypto config returns internal objects.
                // As we cannot create those internal objects, we are returning null.
                // If no custom algorithm is plugged-in, at least these two algorithms
                // will be inside the delegate dictionary.
                case SecurityAlgorithms.Sha1Digest:
                    return SHA1.Create();
                case SecurityAlgorithms.ExclusiveC14n:
                    throw new PlatformNotSupportedException();
                case SHA256String:
                case SecurityAlgorithms.Sha256Digest:
                    return SHA256.Create();
                case SecurityAlgorithms.Sha512Digest:
                    return SHA512.Create();
                case SecurityAlgorithms.Aes128Encryption:
                case SecurityAlgorithms.Aes192Encryption:
                case SecurityAlgorithms.Aes256Encryption:
                case SecurityAlgorithms.Aes128KeyWrap:
                case SecurityAlgorithms.Aes192KeyWrap:
                case SecurityAlgorithms.Aes256KeyWrap:
                    return Aes.Create();
                case SecurityAlgorithms.TripleDesEncryption:
                case SecurityAlgorithms.TripleDesKeyWrap:
                    return TripleDES.Create();
                case SecurityAlgorithms.HmacSha1Signature:
                    return new HMACSHA1();
                case SecurityAlgorithms.HmacSha256Signature:
                    return new HMACSHA256();
                case SecurityAlgorithms.ExclusiveC14nWithComments:
                    throw new PlatformNotSupportedException();
                case SecurityAlgorithms.Ripemd160Digest:
                    return null;
                case SecurityAlgorithms.DesEncryption:
                    return DES.Create();
                default:
                    return null;
            }
        }

        internal static object GetAlgorithmFromConfig(string algorithm)
        {
            if (string.IsNullOrEmpty(algorithm))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(algorithm)));
            }

            object algorithmObject = null;
            object defaultObject = null;

            if (!s_algorithmDelegateDictionary.TryGetValue(algorithm, out Func<object> delegateFunction))
            {
                lock (s_algorithmDictionaryLock)
                {
                    if (!s_algorithmDelegateDictionary.ContainsKey(algorithm))
                    {
                        try
                        {
                            algorithmObject = CryptoConfig.CreateFromName(algorithm);
                        }
                        catch (TargetInvocationException)
                        {
                            s_algorithmDelegateDictionary[algorithm] = null;
                        }

                        if (algorithmObject == null)
                        {
                            s_algorithmDelegateDictionary[algorithm] = null;
                        }
                        else
                        {
                            defaultObject = GetDefaultAlgorithm(algorithm);
                            if (defaultObject != null && defaultObject.GetType() == algorithmObject.GetType())
                            {
                                s_algorithmDelegateDictionary[algorithm] = null;
                            }
                            else
                            {
                                // Create a factory delegate which returns new instances of the algorithm type for later calls.
                                Type algorithmType = algorithmObject.GetType();
                                System.Linq.Expressions.NewExpression algorithmCreationExpression = Linq.Expressions.Expression.New(algorithmType);
                                Linq.Expressions.LambdaExpression creationFunction = Linq.Expressions.Expression.Lambda<Func<object>>(algorithmCreationExpression);
                                delegateFunction = creationFunction.Compile() as Func<object>;

                                if (delegateFunction != null)
                                {
                                    s_algorithmDelegateDictionary[algorithm] = delegateFunction;
                                }
                                return algorithmObject;
                            }
                        }
                    }
                }
            }
            else
            {
                if (delegateFunction != null)
                {
                    return delegateFunction.Invoke();
                }
            }

            //
            // This is a fallback in case CryptoConfig fails to return a valid
            // algorithm object. CrytoConfig does not understand all the uri's and
            // can return a null in that case, in which case it is our responsibility
            // to fallback and create the right algorithm if it is a uri we understand
            //
            switch (algorithm)
            {
                case SHA256String:
                case SecurityAlgorithms.Sha256Digest:
                    return SHA256.Create();
                case SecurityAlgorithms.Sha1Digest:
                    return SHA1.Create();
                case SecurityAlgorithms.HmacSha1Signature:
                    return new HMACSHA1();
                default:
                    break;
            }

            return null;
        }

        internal static HashAlgorithm NewSha1HashAlgorithm()
        {
            return CreateHashAlgorithm(SecurityAlgorithms.Sha1Digest);
        }

        internal static HashAlgorithm NewSha256HashAlgorithm()
        {
            return CreateHashAlgorithm(SecurityAlgorithms.Sha256Digest);
        }

        internal static KeyedHashAlgorithm NewHmacSha1KeyedHashAlgorithm(byte[] key)
        {
            return CreateKeyedHashAlgorithm(key, SecurityAlgorithms.HmacSha1Signature);
        }
    }
}

