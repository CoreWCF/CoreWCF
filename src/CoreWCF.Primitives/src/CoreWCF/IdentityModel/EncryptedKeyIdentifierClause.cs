// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;

namespace CoreWCF.IdentityModel.Tokens
{
    public sealed class EncryptedKeyIdentifierClause : BinaryKeyIdentifierClause
    {
        public EncryptedKeyIdentifierClause(byte[] encryptedKey, string encryptionMethod)
            : this(encryptedKey, encryptionMethod, null)
        {
        }

        public EncryptedKeyIdentifierClause(byte[] encryptedKey, string encryptionMethod, SecurityKeyIdentifier encryptingKeyIdentifier)
            : this(encryptedKey, encryptionMethod, encryptingKeyIdentifier, null)
        {
        }

        public EncryptedKeyIdentifierClause(byte[] encryptedKey, string encryptionMethod, SecurityKeyIdentifier encryptingKeyIdentifier, string carriedKeyName)
            : this(encryptedKey, encryptionMethod, encryptingKeyIdentifier, carriedKeyName, true, null, 0)
        {
        }

        public EncryptedKeyIdentifierClause(byte[] encryptedKey, string encryptionMethod, SecurityKeyIdentifier encryptingKeyIdentifier, string carriedKeyName, byte[] derivationNonce, int derivationLength)
            : this(encryptedKey, encryptionMethod, encryptingKeyIdentifier, carriedKeyName, true, derivationNonce, derivationLength)
        {
        }

        internal EncryptedKeyIdentifierClause(byte[] encryptedKey, string encryptionMethod, SecurityKeyIdentifier encryptingKeyIdentifier, string carriedKeyName, bool cloneBuffer, byte[] derivationNonce, int derivationLength)
            : base("http://www.w3.org/2001/04/xmlenc#EncryptedKey", encryptedKey, cloneBuffer, derivationNonce, derivationLength)
        {
            CarriedKeyName = carriedKeyName;
            EncryptionMethod = encryptionMethod ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(encryptionMethod));
            EncryptingKeyIdentifier = encryptingKeyIdentifier;
        }

        public string CarriedKeyName { get; }

        public SecurityKeyIdentifier EncryptingKeyIdentifier { get; }

        public string EncryptionMethod { get; }

        public override bool Matches(SecurityKeyIdentifierClause keyIdentifierClause)
        {
            EncryptedKeyIdentifierClause that = keyIdentifierClause as EncryptedKeyIdentifierClause;
            return ReferenceEquals(this, that) || (that != null && that.Matches(GetRawBuffer(), EncryptionMethod, CarriedKeyName));
        }

        public bool Matches(byte[] encryptedKey, string encryptionMethod, string carriedKeyName)
        {
            return Matches(encryptedKey) && EncryptionMethod == encryptionMethod && CarriedKeyName == carriedKeyName;
        }

        public byte[] GetEncryptedKey()
        {
            return GetBuffer();
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "EncryptedKeyIdentifierClause(EncryptedKey = {0}, Method '{1}')",
                Convert.ToBase64String(GetRawBuffer()), EncryptionMethod);
        }
    }
}
