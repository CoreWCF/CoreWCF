// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.IdentityModel.Tokens;

namespace CoreWCF.IdentityModel.Tokens.Saml
{
    public class KeyInfo : Microsoft.IdentityModel.Xml.KeyInfo
    {
        public SecurityTokenReference SecurityTokenReference
        {
            get;
            set;
        }

        public BinarySecret BinarySecret
        {
            get;
            set;
        }

        public EncryptedKey EncryptedKey
        {
            get;
            set;
        }

        protected override bool MatchesKey(Microsoft.IdentityModel.Tokens.SecurityKey key)
        {
            X509SecurityKey x509SecurityKey = key as X509SecurityKey;
            if (key != null)
            {
                if (SecurityTokenReference != null)
                {
                    return SecurityTokenReference.MatchesKey(key);
                }
            }

            return base.MatchesKey(key);
        }

        public override bool Equals(object obj)
        {
            // Check if obj is null or not the same type
            if (obj == null || GetType() != obj.GetType())
                return false;

            // Check if it's the same instance
            if (ReferenceEquals(this, obj))
                return true;

            KeyInfo other = (KeyInfo)obj;

            // Call base class Equals
            if (!base.Equals(obj))
                return false;

            // Compare SecurityTokenReference
            if (!Object.Equals(SecurityTokenReference, other.SecurityTokenReference))
                return false;

            // Compare BinarySecret
            if (!Object.Equals(BinarySecret, other.BinarySecret))
                return false;

            // Compare EncryptedKey
            if (!Object.Equals(EncryptedKey, other.EncryptedKey))
                return false;

            return true;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode() * 23 + HashCode.Combine(SecurityTokenReference, BinarySecret, EncryptedKey);
        }
    }
}

