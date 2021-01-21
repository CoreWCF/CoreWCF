// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;

namespace CoreWCF.IdentityModel.Tokens
{
    sealed class EncryptedKeyHashIdentifierClause : BinaryKeyIdentifierClause
    {
        public EncryptedKeyHashIdentifierClause(byte[] encryptedKeyHash)
            : this(encryptedKeyHash, true)
        {
        }

        internal EncryptedKeyHashIdentifierClause(byte[] encryptedKeyHash, bool cloneBuffer)
            : this(encryptedKeyHash, cloneBuffer, null, 0)
        {
        }

        internal EncryptedKeyHashIdentifierClause(byte[] encryptedKeyHash, bool cloneBuffer, byte[] derivationNonce, int derivationLength)
            : base(null, encryptedKeyHash, cloneBuffer, derivationNonce, derivationLength)
        {
        }

        public byte[] GetEncryptedKeyHash()
        {
            return GetBuffer();
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "EncryptedKeyHashIdentifierClause(Hash = {0})", Convert.ToBase64String(GetRawBuffer()));
        }
    }
}
