// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.IdentityModel.Tokens.Saml
{
    /// <summary>
    /// Represents the EncryptedKey Element of X509Data as per:  https://www.w3.org/TR/2001/PR-xmldsig-core-20010820/#sec-X509Data
    /// </summary>
    public class EncryptedKey
    {
        /// <summary>
        /// Creates an EncryptedKey
        /// </summary>
        public EncryptedKey(string encryptionMethod, Microsoft.IdentityModel.Xml.KeyInfo keyInfo, string cipherData)
        {
            EncryptionMethod = encryptionMethod;
            KeyInfo = keyInfo;
            CipherData = cipherData;
        }

        /// <summary>
        /// Gets the SecurityKeyIdentifier
        /// </summary>
        public string EncryptionMethod { get; }

        /// <summary>
        /// encryptedKey
        /// </summary>
        public Microsoft.IdentityModel.Xml.KeyInfo KeyInfo { get; }

        /// <summary>
        ///
        /// </summary>
        public string CipherData { get; }

        public override bool Equals(object obj)
        {
            if (obj is not EncryptedKey other)
                return false;
            if (other is null)
                return false;
            if (ReferenceEquals(this, other))
                return true;
            return string.Equals(EncryptionMethod, other.EncryptionMethod) &&
                   Equals(KeyInfo, other.KeyInfo) &&
                   string.Equals(CipherData, other.CipherData);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(EncryptionMethod, KeyInfo, CipherData);
        }
    }
}
