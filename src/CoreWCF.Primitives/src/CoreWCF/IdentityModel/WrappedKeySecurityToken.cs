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
        private string id;
        private DateTime effectiveTime;
        private EncryptedKey encryptedKey;
        private ReadOnlyCollection<SecurityKey> securityKey;
        private byte[] wrappedKey;
        private string wrappingAlgorithm;
        private SecurityToken wrappingToken;
        private SecurityKey wrappingSecurityKey;
        private SecurityKeyIdentifier wrappingTokenReference;
        private bool serializeCarriedKeyName;
        private byte[] wrappedKeyHash;
        private XmlDictionaryString wrappingAlgorithmDictionaryString;

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
            this.serializeCarriedKeyName = true;
        }

        private WrappedKeySecurityToken(string id, byte[] keyToWrap, string wrappingAlgorithm, XmlDictionaryString wrappingAlgorithmDictionaryString)
        {
            if (id == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(id));
            if (wrappingAlgorithm == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(wrappingAlgorithm));
            if (keyToWrap == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(keyToWrap));

            this.id = id;
            this.effectiveTime = DateTime.UtcNow;
            this.securityKey = SecurityUtils.CreateSymmetricSecurityKeys(keyToWrap);
            this.wrappingAlgorithm = wrappingAlgorithm;
            this.wrappingAlgorithmDictionaryString = wrappingAlgorithmDictionaryString;
        }

        public override string Id => this.id;

        public override DateTime ValidFrom => this.effectiveTime;

        public override DateTime ValidTo => DateTime.MaxValue;

        internal EncryptedKey EncryptedKey
        {
            get { return this.encryptedKey; }
            set { this.encryptedKey = value; }
        }

        internal ReferenceList ReferenceList => this.encryptedKey == null ? null : this.encryptedKey.ReferenceList;

        public string WrappingAlgorithm => this.wrappingAlgorithm;

        internal SecurityKey WrappingSecurityKey => this.wrappingSecurityKey;

        public SecurityToken WrappingToken => this.wrappingToken;

        public SecurityKeyIdentifier WrappingTokenReference => this.wrappingTokenReference;

        internal string CarriedKeyName => null;

        public override ReadOnlyCollection<SecurityKey> SecurityKeys => this.securityKey;

        internal byte[] GetHash()
        {
            if (this.wrappedKeyHash == null)
            {
                EnsureEncryptedKeySetUp();
                using (HashAlgorithm hash = CryptoHelper.NewSha1HashAlgorithm())
                {
                    this.wrappedKeyHash = hash.ComputeHash(this.encryptedKey.GetWrappedKey());
                }
            }
            return wrappedKeyHash;
        }

        public byte[] GetWrappedKey()
        {
            return SecurityUtils.CloneBuffer(this.wrappedKey);
        }

        internal void EnsureEncryptedKeySetUp()
        {
            if (this.encryptedKey == null)
            {
                EncryptedKey ek = new EncryptedKey();
                ek.Id = this.Id;
                if (this.serializeCarriedKeyName)
                {
                    ek.CarriedKeyName = this.CarriedKeyName;
                }
                else
                {
                    ek.CarriedKeyName = null;
                }
                ek.EncryptionMethod = this.WrappingAlgorithm;
                ek.EncryptionMethodDictionaryString = this.wrappingAlgorithmDictionaryString;
                ek.SetUpKeyWrap(this.wrappedKey);
                if (this.WrappingTokenReference != null)
                {
                    ek.KeyIdentifier = this.WrappingTokenReference;
                }
                this.encryptedKey = ek;
            }
        }

        public override bool CanCreateKeyIdentifierClause<T>()
        {
            if (typeof(T) == typeof(EncryptedKeyHashIdentifierClause))
                return true;

            return base.CanCreateKeyIdentifierClause<T>();
        }

        public override T CreateKeyIdentifierClause<T>()
        {
            if (typeof(T) == typeof(EncryptedKeyHashIdentifierClause))
                return new EncryptedKeyHashIdentifierClause(GetHash()) as T;

            return base.CreateKeyIdentifierClause<T>();
        }

        public override bool MatchesKeyIdentifierClause(SecurityKeyIdentifierClause keyIdentifierClause)
        {
            EncryptedKeyHashIdentifierClause encKeyIdentifierClause = keyIdentifierClause as EncryptedKeyHashIdentifierClause;
            if (encKeyIdentifierClause != null)
                return encKeyIdentifierClause.Matches(GetHash());

            return base.MatchesKeyIdentifierClause(keyIdentifierClause);
        }
    }
}
