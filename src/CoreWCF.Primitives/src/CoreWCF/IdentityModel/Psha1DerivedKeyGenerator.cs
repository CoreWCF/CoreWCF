// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Security.Cryptography;
using CoreWCF.Runtime;

namespace CoreWCF.IdentityModel
{
    internal sealed class Psha1DerivedKeyGenerator
    {
        private readonly byte[] _key;

        public Psha1DerivedKeyGenerator(byte[] key)
        {
            _key = key ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(key));
        }

        public byte[] GenerateDerivedKey(byte[] label, byte[] nonce, int derivedKeySize, int position)
        {
            if (label == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(label));
            }
            if (nonce == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(nonce));
            }
            ManagedPsha1 dkcp = new ManagedPsha1(_key, label, nonce);
            return dkcp.GetDerivedKey(derivedKeySize, position);
        }

        // private class to do the real work
        // Note: Though named ManagedPsha1, this works for both fips and non-fips compliance
        private sealed class ManagedPsha1
        {
            private byte[] _aValue;
            private readonly byte[] _buffer;
            private byte[] _chunk;
            private readonly KeyedHashAlgorithm _hmac;
            private int _index;
            private int _position;
            private readonly byte[] _secret;
            private readonly byte[] _seed;

            // assume arguments are already validated
            public ManagedPsha1(byte[] secret, byte[] label, byte[] seed)
            {
                _secret = secret;
                _seed = Fx.AllocateByteArray(checked(label.Length + seed.Length));
                label.CopyTo(_seed, 0);
                seed.CopyTo(_seed, label.Length);

                _aValue = _seed;
                _chunk = Array.Empty<byte>();
                _index = 0;
                _position = 0;
                _hmac = CryptoHelper.NewHmacSha1KeyedHashAlgorithm(secret);

                _buffer = Fx.AllocateByteArray(checked(_hmac.HashSize / 8 + _seed.Length));
            }

            public byte[] GetDerivedKey(int derivedKeySize, int position)
            {
                if (derivedKeySize < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(derivedKeySize), SRCommon.ValueMustBeNonNegative));
                }
                if (_position > position)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(position), SR.Format(SR.ValueMustBeInRange, 0, _position)));
                }

                // Seek to the desired position in the pseudo-random stream.
                while (_position < position)
                {
                    GetByte();
                }
                int sizeInBytes = derivedKeySize / 8;
                byte[] derivedKey = new byte[sizeInBytes];
                for (int i = 0; i < sizeInBytes; i++)
                {
                    derivedKey[i] = GetByte();
                }
                return derivedKey;
            }

            private byte GetByte()
            {
                if (_index >= _chunk.Length)
                {
                    // Calculate A(i) = HMAC_SHA1(secret, A(i-1)).
                    _hmac.Initialize();
                    _aValue = _hmac.ComputeHash(_aValue);
                    // Calculate P_SHA1(secret, seed)[j] = HMAC_SHA1(secret, A(j+1) || seed).
                    _aValue.CopyTo(_buffer, 0);
                    _seed.CopyTo(_buffer, _aValue.Length);
                    _hmac.Initialize();
                    _chunk = _hmac.ComputeHash(_buffer);
                    _index = 0;
                }
                _position++;
                return _chunk[_index++];
            }
        }
    }
}
