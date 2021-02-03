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
        private readonly string _id;
        private readonly DateTime _effectiveTime;
        private readonly ReadOnlyCollection<SecurityKey> _securityKey;
        private readonly byte[] _wrappedKey;
        private readonly bool _serializeCarriedKeyName;
        private byte[] _wrappedKeyHash;
        private readonly XmlDictionaryString _wrappingAlgorithmDictionaryString;

        internal WrappedKeySecurityToken(string id, byte[] keyToWrap, ISspiNegotiation wrappingSspiContext)
            : this(id, keyToWrap, (wrappingSspiContext != null) ? (wrappingSspiContext.KeyEncryptionAlgorithm) : null, wrappingSspiContext, null)
        {

        }

        internal WrappedKeySecurityToken(string id, byte[] keyToWrap, string wrappingAlgorithm, ISspiNegotiation wrappingSspiContext, byte[] wrappedKey)
        : this(id, keyToWrap, wrappingAlgorithm, null)
        {
            if (wrappingSspiContext == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(wrappingSspiContext));
            }
            //this.wrappingSspiContext = wrappingSspiContext;
            if (wrappedKey == null)
            {
                _wrappedKey = wrappingSspiContext.Encrypt(keyToWrap);
            }
            else
            {
                _wrappedKey = wrappedKey;
            }
           _serializeCarriedKeyName = false;
        }

        internal WrappedKeySecurityToken(string id, byte[] keyToWrap, string wrappingAlgorithm, XmlDictionaryString wrappingAlgorithmDictionaryString, SecurityToken wrappingToken, SecurityKeyIdentifier wrappingTokenReference)
            : this(id, keyToWrap, wrappingAlgorithm, wrappingAlgorithmDictionaryString, wrappingToken, wrappingTokenReference, null, null)
        {
        }

        internal WrappedKeySecurityToken(string id, byte[] keyToWrap, string wrappingAlgorithm, XmlDictionaryString wrappingAlgorithmDictionaryString, SecurityToken wrappingToken, SecurityKeyIdentifier wrappingTokenReference, byte[] wrappedKey, SecurityKey wrappingSecurityKey)
             : this(id, keyToWrap, wrappingAlgorithm, wrappingAlgorithmDictionaryString)
        {
            WrappingToken = wrappingToken ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(wrappingToken));
            WrappingTokenReference = wrappingTokenReference;
            if (wrappedKey == null)
            {
                _wrappedKey = SecurityUtils.EncryptKey(wrappingToken, wrappingAlgorithm, keyToWrap);
            }
            else
            {
                _wrappedKey = wrappedKey;
            }
            WrappingSecurityKey = wrappingSecurityKey;
            _serializeCarriedKeyName = true;
        }

        private WrappedKeySecurityToken(string id, byte[] keyToWrap, string wrappingAlgorithm, XmlDictionaryString wrappingAlgorithmDictionaryString)
        {
            if (keyToWrap == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(keyToWrap));
            }

            _id = id ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(id));
            _effectiveTime = DateTime.UtcNow;
            _securityKey = SecurityUtils.CreateSymmetricSecurityKeys(keyToWrap);
            WrappingAlgorithm = wrappingAlgorithm ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(wrappingAlgorithm));
            _wrappingAlgorithmDictionaryString = wrappingAlgorithmDictionaryString;
        }

        internal WrappedKeySecurityToken(
          string id,
          byte[] keyToWrap,
          string wrappingAlgorithm,
          SecurityToken wrappingToken,
          SecurityKeyIdentifier wrappingTokenReference,
          byte[] wrappedKey,
          SecurityKey wrappingSecurityKey)
          : this(id, keyToWrap, wrappingAlgorithm, (XmlDictionaryString)null, wrappingToken, wrappingTokenReference, wrappedKey, wrappingSecurityKey)
        {
        }

        public override string Id => _id;

        public override DateTime ValidFrom => _effectiveTime;

        public override DateTime ValidTo => DateTime.MaxValue;

        internal EncryptedKey EncryptedKey { get; set; }

        internal ReferenceList ReferenceList => EncryptedKey?.ReferenceList;

        public string WrappingAlgorithm { get; }

        internal SecurityKey WrappingSecurityKey { get; }

        public SecurityToken WrappingToken { get; }

        public SecurityKeyIdentifier WrappingTokenReference { get; }

        internal string CarriedKeyName => null;

        public override ReadOnlyCollection<SecurityKey> SecurityKeys => _securityKey;

        internal byte[] GetHash()
        {
            if (_wrappedKeyHash == null)
            {
                EnsureEncryptedKeySetUp();
                using (HashAlgorithm hash = CryptoHelper.NewSha1HashAlgorithm())
                {
                    _wrappedKeyHash = hash.ComputeHash(EncryptedKey.GetWrappedKey());
                }
            }
            return _wrappedKeyHash;
        }

        public byte[] GetWrappedKey()
        {
            return SecurityUtils.CloneBuffer(_wrappedKey);
        }

        internal void EnsureEncryptedKeySetUp()
        {
            if (EncryptedKey == null)
            {
                EncryptedKey ek = new EncryptedKey
                {
                    Id = Id
                };
                if (_serializeCarriedKeyName)
                {
                    ek.CarriedKeyName = CarriedKeyName;
                }
                else
                {
                    ek.CarriedKeyName = null;
                }
                ek.EncryptionMethod = WrappingAlgorithm;
                ek.EncryptionMethodDictionaryString = _wrappingAlgorithmDictionaryString;
                ek.SetUpKeyWrap(_wrappedKey);
                if (WrappingTokenReference != null)
                {
                    ek.KeyIdentifier = WrappingTokenReference;
                }
                EncryptedKey = ek;
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
            if (keyIdentifierClause is EncryptedKeyHashIdentifierClause encKeyIdentifierClause)
            {
                return encKeyIdentifierClause.Matches(GetHash());
            }

            return base.MatchesKeyIdentifierClause(keyIdentifierClause);
        }
    }
}
