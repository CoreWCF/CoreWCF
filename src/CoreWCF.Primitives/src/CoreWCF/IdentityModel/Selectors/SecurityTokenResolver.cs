// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using CoreWCF.IdentityModel.Tokens;

namespace CoreWCF.IdentityModel.Selectors
{
    // TODO: Evaluate removing this class and all usages as the full framework implementation has a lot more functionality.
    public abstract class SecurityTokenResolver
    {
        public SecurityToken ResolveToken(SecurityKeyIdentifier keyIdentifier)
        {
            if (keyIdentifier == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(keyIdentifier));
            }
            SecurityToken token;
            if (!this.TryResolveTokenCore(keyIdentifier, out token))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new InvalidOperationException(SR.Format("UnableToResolveTokenReference", keyIdentifier)));
            }
            return token;
        }

        public bool TryResolveToken(SecurityKeyIdentifier keyIdentifier, out SecurityToken token)
        {
            if (keyIdentifier == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(keyIdentifier));
            }
            return TryResolveTokenCore(keyIdentifier, out token);
        }

        public SecurityToken ResolveToken(SecurityKeyIdentifierClause keyIdentifierClause)
        {
            if (keyIdentifierClause == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(keyIdentifierClause));
            }
            SecurityToken token;
            if (!this.TryResolveTokenCore(keyIdentifierClause, out token))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new InvalidOperationException(SR.Format("UnableToResolveTokenReference", keyIdentifierClause)));
            }
            return token;
        }

        public bool TryResolveToken(SecurityKeyIdentifierClause keyIdentifierClause, out SecurityToken token)
        {
            if (keyIdentifierClause == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(keyIdentifierClause));
            }
            return this.TryResolveTokenCore(keyIdentifierClause, out token);
        }

        public SecurityKey ResolveSecurityKey(SecurityKeyIdentifierClause keyIdentifierClause)
        {
            if (keyIdentifierClause == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(keyIdentifierClause));
            }
            SecurityKey key;
            if (!this.TryResolveSecurityKeyCore(keyIdentifierClause, out key))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new InvalidOperationException(SR.Format("UnableToResolveKeyReference", keyIdentifierClause)));
            }
            return key;
        }

        public bool TryResolveSecurityKey(SecurityKeyIdentifierClause keyIdentifierClause, out SecurityKey key)
        {
            if (keyIdentifierClause == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(keyIdentifierClause));
            }
            return this.TryResolveSecurityKeyCore(keyIdentifierClause, out key);
        }

        // protected methods
        protected abstract bool TryResolveTokenCore(SecurityKeyIdentifier keyIdentifier, out SecurityToken token);
        protected abstract bool TryResolveTokenCore(SecurityKeyIdentifierClause keyIdentifierClause, out SecurityToken token);
        protected abstract bool TryResolveSecurityKeyCore(SecurityKeyIdentifierClause keyIdentifierClause, out SecurityKey key);

        public static SecurityTokenResolver CreateDefaultSecurityTokenResolver(ReadOnlyCollection<SecurityToken> tokens, bool canMatchLocalId)
        {
            return new SimpleTokenResolver(tokens, canMatchLocalId);
        }

        private class SimpleTokenResolver : SecurityTokenResolver
        {
            private ReadOnlyCollection<SecurityToken> tokens;
            private bool canMatchLocalId;

            public SimpleTokenResolver(ReadOnlyCollection<SecurityToken> tokens, bool canMatchLocalId)
            {
                if (tokens == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(tokens));

                this.tokens = tokens;
                this.canMatchLocalId = canMatchLocalId;
            }

            protected override bool TryResolveSecurityKeyCore(SecurityKeyIdentifierClause keyIdentifierClause, out SecurityKey key)
            {
                if (keyIdentifierClause == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(keyIdentifierClause));

                key = null;
                for (int i = 0; i < this.tokens.Count; ++i)
                {
                    SecurityKey securityKey = this.tokens[i].ResolveKeyIdentifierClause(keyIdentifierClause);
                    if (securityKey != null)
                    {
                        key = securityKey;
                        return true;
                    }
                }

                if (keyIdentifierClause is EncryptedKeyIdentifierClause)
                {
                    EncryptedKeyIdentifierClause keyClause = (EncryptedKeyIdentifierClause)keyIdentifierClause;
                    SecurityKeyIdentifier keyIdentifier = keyClause.EncryptingKeyIdentifier;
                    if (keyIdentifier != null && keyIdentifier.Count > 0)
                    {
                        for (int i = 0; i < keyIdentifier.Count; i++)
                        {
                            SecurityKey unwrappingSecurityKey = null;
                            if (TryResolveSecurityKey(keyIdentifier[i], out unwrappingSecurityKey))
                            {
                                byte[] wrappedKey = keyClause.GetEncryptedKey();
                                string wrappingAlgorithm = keyClause.EncryptionMethod;
                                byte[] unwrappedKey = unwrappingSecurityKey.DecryptKey(wrappingAlgorithm, wrappedKey);
                                key = new InMemorySymmetricSecurityKey(unwrappedKey, false);
                                return true;
                            }
                        }
                    }
                }

                return key != null;
            }

            protected override bool TryResolveTokenCore(SecurityKeyIdentifier keyIdentifier, out SecurityToken token)
            {
                if (keyIdentifier == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(keyIdentifier));

                token = null;
                for (int i = 0; i < keyIdentifier.Count; ++i)
                {

                    SecurityToken securityToken = ResolveSecurityToken(keyIdentifier[i]);
                    if (securityToken != null)
                    {
                        token = securityToken;
                        break;
                    }
                }

                return (token != null);
            }

            protected override bool TryResolveTokenCore(SecurityKeyIdentifierClause keyIdentifierClause, out SecurityToken token)
            {
                if (keyIdentifierClause == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(keyIdentifierClause));

                token = null;

                SecurityToken securityToken = ResolveSecurityToken(keyIdentifierClause);
                if (securityToken != null)
                    token = securityToken;

                return (token != null);
            }

            private SecurityToken ResolveSecurityToken(SecurityKeyIdentifierClause keyIdentifierClause)
            {
                if (keyIdentifierClause == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(keyIdentifierClause));

                if (!this.canMatchLocalId && keyIdentifierClause is LocalIdKeyIdentifierClause)
                    return null;

                for (int i = 0; i < this.tokens.Count; ++i)
                {
                    if (this.tokens[i].MatchesKeyIdentifierClause(keyIdentifierClause))
                        return this.tokens[i];
                }

                return null;
            }
        }
    }
}
