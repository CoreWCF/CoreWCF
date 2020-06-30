using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

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
