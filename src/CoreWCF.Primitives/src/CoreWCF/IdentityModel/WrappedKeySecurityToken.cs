// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Xml;
using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Tokens;

namespace CoreWCF.Security.Tokens
{
    public class WrappedKeySecurityToken : SecurityToken
    {
        private readonly string id;
        private readonly DateTime effectiveTime;
        private EncryptedKey encryptedKey;
        private readonly ReadOnlyCollection<SecurityKey> securityKey;
        private readonly byte[] wrappedKey;
        private readonly string wrappingAlgorithm;
        private readonly SecurityToken wrappingToken;
        private readonly SecurityKey wrappingSecurityKey;
        private readonly SecurityKeyIdentifier wrappingTokenReference;
        private readonly bool serializeCarriedKeyName;
        private byte[] wrappedKeyHash;
        private readonly XmlDictionaryString wrappingAlgorithmDictionaryString;

        internal WrappedKeySecurityToken(string id, byte[] keyToWrap, string wrappingAlgorithm, XmlDictionaryString wrappingAlgorithmDictionaryString, SecurityToken wrappingToken, SecurityKeyIdentifier wrappingTokenReference)
            : this(id, keyToWrap, wrappingAlgorithm, wrappingAlgorithmDictionaryString, wrappingToken, wrappingTokenReference, null, null)
        {
        }

        internal WrappedKeySecurityToken(string id, byte[] keyToWrap, string wrappingAlgorithm, XmlDictionaryString wrappingAlgorithmDictionaryString, SecurityToken wrappingToken, SecurityKeyIdentifier wrappingTokenReference, byte[] wrappedKey, SecurityKey wrappingSecurityKey)
             : this(id, keyToWrap, wrappingAlgorithm, wrappingAlgorithmDictionaryString)
        {
            if (wrappingToken == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("wrappingToken");
            }
            this.wrappingToken = wrappingToken;
            this.wrappingTokenReference = wrappingTokenReference;
            if (wrappedKey == null)
            {
                this.wrappedKey = SecurityUtils.EncryptKey(wrappingToken, wrappingAlgorithm, keyToWrap);
            }
            else
            {
                this.wrappedKey = wrappedKey;
            }
            this.wrappingSecurityKey = wrappingSecurityKey;
            serializeCarriedKeyName = true;
        }

        private WrappedKeySecurityToken(string id, byte[] keyToWrap, string wrappingAlgorithm, XmlDictionaryString wrappingAlgorithmDictionaryString)
        {
            if (id == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(id));
            }

            if (wrappingAlgorithm == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(wrappingAlgorithm));
            }

            if (keyToWrap == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(keyToWrap));
            }

            this.id = id;
            effectiveTime = DateTime.UtcNow;
            securityKey = SecurityUtils.CreateSymmetricSecurityKeys(keyToWrap);
            this.wrappingAlgorithm = wrappingAlgorithm;
            this.wrappingAlgorithmDictionaryString = wrappingAlgorithmDictionaryString;
        }

        public override string Id => id;

        public override DateTime ValidFrom => effectiveTime;

        public override DateTime ValidTo => DateTime.MaxValue;

        internal EncryptedKey EncryptedKey
        {
            get { return encryptedKey; }
            set { encryptedKey = value; }
        }

        internal ReferenceList ReferenceList => encryptedKey == null ? null : encryptedKey.ReferenceList;

        public string WrappingAlgorithm => wrappingAlgorithm;

        internal SecurityKey WrappingSecurityKey => wrappingSecurityKey;

        public SecurityToken WrappingToken => wrappingToken;

        public SecurityKeyIdentifier WrappingTokenReference => wrappingTokenReference;

        internal string CarriedKeyName => null;

        public override ReadOnlyCollection<SecurityKey> SecurityKeys => securityKey;

        internal byte[] GetHash()
        {
            if (wrappedKeyHash == null)
            {
                EnsureEncryptedKeySetUp();
                using (HashAlgorithm hash = CryptoHelper.NewSha1HashAlgorithm())
                {
                    wrappedKeyHash = hash.ComputeHash(encryptedKey.GetWrappedKey());
                }
            }
            return wrappedKeyHash;
        }

        public byte[] GetWrappedKey()
        {
            return SecurityUtils.CloneBuffer(wrappedKey);
        }

        internal void EnsureEncryptedKeySetUp()
        {
            if (encryptedKey == null)
            {
                EncryptedKey ek = new EncryptedKey();
                ek.Id = Id;
                if (serializeCarriedKeyName)
                {
                    ek.CarriedKeyName = CarriedKeyName;
                }
                else
                {
                    ek.CarriedKeyName = null;
                }
                ek.EncryptionMethod = WrappingAlgorithm;
                ek.EncryptionMethodDictionaryString = wrappingAlgorithmDictionaryString;
                ek.SetUpKeyWrap(wrappedKey);
                if (WrappingTokenReference != null)
                {
                    ek.KeyIdentifier = WrappingTokenReference;
                }
                encryptedKey = ek;
            }
        }

        public override bool CanCreateKeyIdentifierClause<T>()
        {
            if (typeof(T) == typeof(EncryptedKeyHashIdentifierClause))
            {
                return true;
            }

            return base.CanCreateKeyIdentifierClause<T>();
        }

        public override T CreateKeyIdentifierClause<T>()
        {
            if (typeof(T) == typeof(EncryptedKeyHashIdentifierClause))
            {
                return new EncryptedKeyHashIdentifierClause(GetHash()) as T;
            }

            return base.CreateKeyIdentifierClause<T>();
        }

        public override bool MatchesKeyIdentifierClause(SecurityKeyIdentifierClause keyIdentifierClause)
        {
            EncryptedKeyHashIdentifierClause encKeyIdentifierClause = keyIdentifierClause as EncryptedKeyHashIdentifierClause;
            if (encKeyIdentifierClause != null)
            {
                return encKeyIdentifierClause.Matches(GetHash());
            }

            return base.MatchesKeyIdentifierClause(keyIdentifierClause);
        }
    }
}
