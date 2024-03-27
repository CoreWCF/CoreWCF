// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Security.Cryptography;

namespace CoreWCF.IdentityModel.Tokens
{
    public class InMemorySymmetricSecurityKey : SymmetricSecurityKey
    {
        private readonly int _keySize;
        private readonly byte[] _symmetricKey;

        public InMemorySymmetricSecurityKey(byte[] symmetricKey)
            : this(symmetricKey, true)
        {
        }

        public InMemorySymmetricSecurityKey(byte[] symmetricKey, bool cloneBuffer)
        {
            if (symmetricKey == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(symmetricKey)));
            }

            if (symmetricKey.Length == 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.Format("SymmetricKeyLengthTooShort", symmetricKey.Length)));
            }
            _keySize = symmetricKey.Length * 8;

            if (cloneBuffer)
            {
                _symmetricKey = new byte[symmetricKey.Length];
                Buffer.BlockCopy(symmetricKey, 0, _symmetricKey, 0, symmetricKey.Length);
            }
            else
            {
                _symmetricKey = symmetricKey;
            }
        }

        public override int KeySize
        {
            get { return _keySize; }
        }

        public override byte[] DecryptKey(string algorithm, byte[] keyData)
        {
            return CryptoHelper.UnwrapKey(_symmetricKey, keyData, algorithm);
        }

        public override byte[] EncryptKey(string algorithm, byte[] keyData)
        {
            return CryptoHelper.WrapKey(_symmetricKey, keyData, algorithm);
        }

        public override byte[] GenerateDerivedKey(string algorithm, byte[] label, byte[] nonce, int derivedKeyLength, int offset)
        {
            return CryptoHelper.GenerateDerivedKey(_symmetricKey, algorithm, label, nonce, derivedKeyLength, offset);
        }

        public override ICryptoTransform GetDecryptionTransform(string algorithm, byte[] iv)
        {
            return CryptoHelper.CreateDecryptor(_symmetricKey, iv, algorithm);
        }

        public override ICryptoTransform GetEncryptionTransform(string algorithm, byte[] iv)
        {
            return CryptoHelper.CreateEncryptor(_symmetricKey, iv, algorithm);
        }

        public override int GetIVSize(string algorithm)
        {
            return CryptoHelper.GetIVSize(algorithm);
        }

        public override KeyedHashAlgorithm GetKeyedHashAlgorithm(string algorithm)
        {
            return CryptoHelper.CreateKeyedHashAlgorithm(_symmetricKey, algorithm);
        }

        public override SymmetricAlgorithm GetSymmetricAlgorithm(string algorithm)
        {
            return CryptoHelper.GetSymmetricAlgorithm(_symmetricKey, algorithm);
        }

        public override byte[] GetSymmetricKey()
        {
            byte[] local = new byte[_symmetricKey.Length];
            Buffer.BlockCopy(_symmetricKey, 0, local, 0, _symmetricKey.Length);

            return local;
        }

        public override bool IsSupportedAlgorithm(string algorithm)
        {
            return (CryptoHelper.IsSymmetricSupportedAlgorithm(algorithm, KeySize));
        }
    }
}
