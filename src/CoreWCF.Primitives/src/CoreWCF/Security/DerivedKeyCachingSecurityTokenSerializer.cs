// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.ObjectModel;
using CoreWCF;
using System.Xml;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.IdentityModel.Selectors;
using System.Collections.Generic;
using CoreWCF.Security.Tokens;
using System;
using CoreWCF.IdentityModel;

namespace CoreWCF.Security
{
    internal class DerivedKeyCachingSecurityTokenSerializer : SecurityTokenSerializer
    {
        private DerivedKeySecurityTokenCache[] cachedTokens;
        private WSSecureConversation secureConversation;
        private SecurityTokenSerializer innerTokenSerializer;
        private bool isInitiator;
        private int indexToCache = 0;
        private Object thisLock;

        internal DerivedKeyCachingSecurityTokenSerializer(int cacheSize, bool isInitiator, WSSecureConversation secureConversation, SecurityTokenSerializer innerTokenSerializer)
            : base()
        {
            if (innerTokenSerializer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("innerTokenSerializer");
            }
            if (secureConversation == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("secureConversation");
            }
            if (cacheSize <= 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException("cacheSize", SR.Format(SR.ValueMustBeGreaterThanZero)));
            }
            cachedTokens = new DerivedKeySecurityTokenCache[cacheSize];
            this.isInitiator = isInitiator;
            this.secureConversation = secureConversation;
            this.innerTokenSerializer = innerTokenSerializer;
            thisLock = new Object();
        }

        protected override bool CanReadKeyIdentifierClauseCore(XmlReader reader)
        {
            return innerTokenSerializer.CanReadKeyIdentifierClause(reader);
        }

        protected override bool CanReadKeyIdentifierCore(XmlReader reader)
        {
            return innerTokenSerializer.CanReadKeyIdentifier(reader);
        }

        protected override bool CanReadTokenCore(XmlReader reader)
        {
            return innerTokenSerializer.CanReadToken(reader);
        }

        protected override SecurityToken ReadTokenCore(XmlReader reader, SecurityTokenResolver tokenResolver)
        {
            XmlDictionaryReader dictionaryReader = XmlDictionaryReader.CreateDictionaryReader(reader);
            if (secureConversation.IsAtDerivedKeyToken(dictionaryReader))
            {
                string id;
                string derivationAlgorithm;
                string label;
                int length;
                byte[] nonce;
                int offset;
                int generation;
                SecurityKeyIdentifierClause tokenToDeriveIdentifier;
                SecurityToken tokenToDerive;
                secureConversation.ReadDerivedKeyTokenParameters(dictionaryReader, tokenResolver, out id, out derivationAlgorithm, out label,
                    out length, out nonce, out offset, out generation, out tokenToDeriveIdentifier, out tokenToDerive);

                DerivedKeySecurityToken cachedToken = GetCachedToken(id, generation, offset, length, label, nonce, tokenToDerive, tokenToDeriveIdentifier, derivationAlgorithm);
                if (cachedToken != null)
                {
                    return cachedToken;
                }

                lock (thisLock)
                {
                    cachedToken = GetCachedToken(id, generation, offset, length, label, nonce, tokenToDerive, tokenToDeriveIdentifier, derivationAlgorithm);
                    if (cachedToken != null)
                    {
                        return cachedToken;
                    }
                    SecurityToken result = secureConversation.CreateDerivedKeyToken( id, derivationAlgorithm, label, length, nonce, offset, generation, tokenToDeriveIdentifier, tokenToDerive );
                    DerivedKeySecurityToken newToken = result as DerivedKeySecurityToken;
                    if (newToken != null)
                    {
                        int pos = indexToCache;
                        if (indexToCache == int.MaxValue)
                            indexToCache = 0;
                        else
                            indexToCache = (++indexToCache) % cachedTokens.Length;
                        cachedTokens[pos] = new DerivedKeySecurityTokenCache(newToken);
                    }
                    return result;
                }
            }
            else
            {
                return innerTokenSerializer.ReadToken(reader, tokenResolver);
            }
        }

        protected override bool CanWriteKeyIdentifierClauseCore(SecurityKeyIdentifierClause keyIdentifierClause)
        {
            return innerTokenSerializer.CanWriteKeyIdentifierClause(keyIdentifierClause);
        }

        protected override bool CanWriteKeyIdentifierCore(SecurityKeyIdentifier keyIdentifier)
        {
            return innerTokenSerializer.CanWriteKeyIdentifier(keyIdentifier);
        }

        protected override bool CanWriteTokenCore(SecurityToken token)
        {
            return innerTokenSerializer.CanWriteToken(token);
        }

        protected override SecurityKeyIdentifierClause ReadKeyIdentifierClauseCore(XmlReader reader)
        {
            return innerTokenSerializer.ReadKeyIdentifierClause(reader);
        }

        protected override SecurityKeyIdentifier ReadKeyIdentifierCore(XmlReader reader)
        {
            return innerTokenSerializer.ReadKeyIdentifier(reader);
        }

        protected override void WriteKeyIdentifierClauseCore(XmlWriter writer, SecurityKeyIdentifierClause keyIdentifierClause)
        {
            innerTokenSerializer.WriteKeyIdentifierClause(writer, keyIdentifierClause);
        }

        protected override void WriteKeyIdentifierCore(XmlWriter writer, SecurityKeyIdentifier keyIdentifier)
        {
            innerTokenSerializer.WriteKeyIdentifier(writer, keyIdentifier);
        }

        protected override void WriteTokenCore(XmlWriter writer, SecurityToken token)
        {
            innerTokenSerializer.WriteToken(writer, token);
        }

        private bool IsMatch(DerivedKeySecurityTokenCache cachedToken, string id, int generation, int offset, int length,
            string label, byte[] nonce, SecurityToken tokenToDerive, string derivationAlgorithm)
        {
            if ((cachedToken.Generation == generation)
                && (cachedToken.Offset == offset)
                && (cachedToken.Length == length)
                && (cachedToken.Label == label)
                && (cachedToken.KeyDerivationAlgorithm == derivationAlgorithm))
            {
                if (!cachedToken.IsSourceKeyEqual(tokenToDerive))
                {
                    return false;
                }
                // since derived key token keys are delay initialized during security processing, it may be possible
                // that the cached derived key token does not have its keys initialized as yet. If so return false for
                // the match so that the framework doesnt try to reference a null key.
                return (CryptoHelper.IsEqual(cachedToken.Nonce, nonce) && (cachedToken.SecurityKeys != null));
            }
            else
            {
                return false;
            }
        }

        private DerivedKeySecurityToken GetCachedToken(string id, int generation, int offset, int length,
            string label, byte[] nonce, SecurityToken tokenToDerive, SecurityKeyIdentifierClause tokenToDeriveIdentifier, string derivationAlgorithm)
        {
            for (int i = 0; i < cachedTokens.Length; ++i)
            {
                DerivedKeySecurityTokenCache cachedToken = cachedTokens[i];
                if (cachedToken != null && IsMatch(cachedToken, id, generation, offset, length,
                    label, nonce, tokenToDerive, derivationAlgorithm))
                {
                    DerivedKeySecurityToken token = new DerivedKeySecurityToken(generation, offset, length, label, nonce, tokenToDerive,
                        tokenToDeriveIdentifier, derivationAlgorithm, id);
                    token.InitializeDerivedKey(cachedToken.SecurityKeys);
                    return token;
                }
            }
            return null;
        }

        private class DerivedKeySecurityTokenCache
        {
            private byte[] keyToDerive;
            private int generation;
            private int offset;
            private int length;
            private string label;
            private string keyDerivationAlgorithm;
            private byte[] nonce;
            private ReadOnlyCollection<SecurityKey> keys;
            private DerivedKeySecurityToken cachedToken;

            public DerivedKeySecurityTokenCache(DerivedKeySecurityToken cachedToken)
            {
                keyToDerive = ((SymmetricSecurityKey)cachedToken.TokenToDerive.SecurityKeys[0]).GetSymmetricKey();
                generation = cachedToken.Generation;
                offset = cachedToken.Offset;
                length = cachedToken.Length;
                label = cachedToken.Label;
                keyDerivationAlgorithm = cachedToken.KeyDerivationAlgorithm;
                nonce = cachedToken.Nonce;
                this.cachedToken = cachedToken;
            }

            public int Generation
            {
                get { return generation; }
            }

            public int Offset
            {
                get { return offset; }
            }

            public int Length
            {
                get { return length; }
            }

            public string Label
            {
                get { return label; }
            }

            public string KeyDerivationAlgorithm
            {
                get { return keyDerivationAlgorithm; }
            }

            public byte[] Nonce
            {
                get { return nonce; }
            }

            public ReadOnlyCollection<SecurityKey> SecurityKeys
            {
                get
                {
                    // we would need to hold onto the cached token till a hit is obtained because of
                    // the delay initialization of derived key crypto by the security header.
                    lock (this)
                    {
                        if (keys == null)
                        {
                            ReadOnlyCollection<SecurityKey> computedKeys;
                            if (cachedToken.TryGetSecurityKeys(out computedKeys))
                            {
                                keys = computedKeys;
                                cachedToken = null;
                            }
                        }
                    }
                    return keys;
                }
            }

            public bool IsSourceKeyEqual(SecurityToken token)
            {
                if (token.SecurityKeys.Count != 1)
                {
                    return false;
                }
                SymmetricSecurityKey key = token.SecurityKeys[0] as SymmetricSecurityKey;
                if (key == null)
                {
                    return false;
                }
                return CryptoHelper.IsEqual(keyToDerive, key.GetSymmetricKey());
            }
        }
    }
}
