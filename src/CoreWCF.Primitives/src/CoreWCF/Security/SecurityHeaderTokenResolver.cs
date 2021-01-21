// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography.X509Certificates;
using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Security
{
    internal sealed class SecurityHeaderTokenResolver : SecurityTokenResolver
    {
        private const int InitialTokenArraySize = 10;
        private int tokenCount;
        private SecurityTokenEntry[] tokens;
        private SecurityToken expectedWrapper;
        private SecurityTokenParameters expectedWrapperTokenParameters;
        private readonly ReceiveSecurityHeader securityHeader;

        public SecurityHeaderTokenResolver()
            : this(null)
        {
        }

        public SecurityHeaderTokenResolver(ReceiveSecurityHeader securityHeader)
        {
            tokens = new SecurityTokenEntry[InitialTokenArraySize];
            this.securityHeader = securityHeader;
        }

        public SecurityToken ExpectedWrapper
        {
            get { return expectedWrapper; }
            set { expectedWrapper = value; }
        }

        public SecurityTokenParameters ExpectedWrapperTokenParameters
        {
            get { return expectedWrapperTokenParameters; }
            set { expectedWrapperTokenParameters = value; }
        }

        public void Add(SecurityToken token)
        {
            Add(token, SecurityTokenReferenceStyle.Internal, null);
        }

        public void Add(SecurityToken token, SecurityTokenReferenceStyle allowedReferenceStyle, SecurityTokenParameters tokenParameters)
        {
            if (token == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(token));
            }

            if ((allowedReferenceStyle == SecurityTokenReferenceStyle.External) && (tokenParameters == null))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.ResolvingExternalTokensRequireSecurityTokenParameters);
            }

            EnsureCapacityToAddToken();
            tokens[tokenCount++] = new SecurityTokenEntry(token, tokenParameters, allowedReferenceStyle);
        }

        private void EnsureCapacityToAddToken()
        {
            if (tokenCount == tokens.Length)
            {
                SecurityTokenEntry[] newTokens = new SecurityTokenEntry[tokens.Length * 2];
                Array.Copy(tokens, 0, newTokens, 0, tokenCount);
                tokens = newTokens;
            }
        }

        public bool CheckExternalWrapperMatch(SecurityKeyIdentifier keyIdentifier)
        {
            if (expectedWrapper == null || expectedWrapperTokenParameters == null)
            {
                return false;
            }

            for (int i = 0; i < keyIdentifier.Count; i++)
            {
                if (expectedWrapperTokenParameters.MatchesKeyIdentifierClause(expectedWrapper, keyIdentifier[i], SecurityTokenReferenceStyle.External))
                {
                    return true;
                }
            }
            return false;
        }

        internal SecurityToken ResolveToken(SecurityKeyIdentifier keyIdentifier, bool matchOnlyExternalTokens, bool resolveIntrinsicKeyClause)
        {
            if (keyIdentifier == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("keyIdentifier");
            }
            for (int i = 0; i < keyIdentifier.Count; i++)
            {
                SecurityToken token = ResolveToken(keyIdentifier[i], matchOnlyExternalTokens, resolveIntrinsicKeyClause);
                if (token != null)
                {
                    return token;
                }
            }
            return null;
        }

        private SecurityKey ResolveSecurityKeyCore(SecurityKeyIdentifierClause keyIdentifierClause, bool createIntrinsicKeys)
        {
            if (keyIdentifierClause == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException("keyIdentifierClause"));
            }

            SecurityKey securityKey;
            for (int i = 0; i < tokenCount; i++)
            {
                securityKey = tokens[i].Token.ResolveKeyIdentifierClause(keyIdentifierClause);
                if (securityKey != null)
                {
                    return securityKey;
                }
            }

            if (createIntrinsicKeys)
            {

                if (SecurityUtils.TryCreateKeyFromIntrinsicKeyClause(keyIdentifierClause, this, out securityKey))
                {
                    return securityKey;
                }
            }

            return null;
        }

        private bool MatchDirectReference(SecurityToken token, SecurityKeyIdentifierClause keyClause)
        {
            LocalIdKeyIdentifierClause localClause = keyClause as LocalIdKeyIdentifierClause;
            if (localClause == null)
            {
                return false;
            }

            return token.MatchesKeyIdentifierClause(localClause);
        }

        internal SecurityToken ResolveToken(SecurityKeyIdentifierClause keyIdentifierClause, bool matchOnlyExternal, bool resolveIntrinsicKeyClause)
        {
            if (keyIdentifierClause == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("keyIdentifierClause");
            }

            SecurityToken resolvedToken = null;
            for (int i = 0; i < tokenCount; i++)
            {
                if (matchOnlyExternal && tokens[i].AllowedReferenceStyle != SecurityTokenReferenceStyle.External)
                {
                    continue;
                }

                SecurityToken token = tokens[i].Token;
                if (tokens[i].TokenParameters != null && tokens[i].TokenParameters.MatchesKeyIdentifierClause(token, keyIdentifierClause, tokens[i].AllowedReferenceStyle))
                {
                    resolvedToken = token;
                    break;
                }
                else if (tokens[i].TokenParameters == null)
                {
                    // match it according to the allowed reference style
                    if (tokens[i].AllowedReferenceStyle == SecurityTokenReferenceStyle.Internal && MatchDirectReference(token, keyIdentifierClause))
                    {
                        resolvedToken = token;
                        break;
                    }
                }
            }

            if ((resolvedToken == null) && (keyIdentifierClause is EncryptedKeyIdentifierClause))
            {
                EncryptedKeyIdentifierClause keyClause = (EncryptedKeyIdentifierClause)keyIdentifierClause;
                SecurityKeyIdentifier wrappingTokenReference = keyClause.EncryptingKeyIdentifier;
                SecurityToken unwrappingToken;
                if (expectedWrapper != null
                    && CheckExternalWrapperMatch(wrappingTokenReference))
                {
                    unwrappingToken = expectedWrapper;
                }
                else
                {
                    unwrappingToken = ResolveToken(wrappingTokenReference, true, resolveIntrinsicKeyClause);
                }

                if (unwrappingToken != null)
                {
                    resolvedToken = SecurityUtils.CreateTokenFromEncryptedKeyClause(keyClause, unwrappingToken);
                }
            }
            if ((resolvedToken == null) && (keyIdentifierClause is X509RawDataKeyIdentifierClause) && (!matchOnlyExternal) && (resolveIntrinsicKeyClause))
            {
                resolvedToken = new X509SecurityToken(new X509Certificate2(((X509RawDataKeyIdentifierClause)keyIdentifierClause).GetX509RawData()));
            }
            byte[] derivationNonce = keyIdentifierClause.GetDerivationNonce();
            if ((resolvedToken != null) && (derivationNonce != null))
            {
                // A Implicit Derived Key is specified. Create a derived key off of the resolve token.
                if (SecurityUtils.GetSecurityKey<SymmetricSecurityKey>(resolvedToken) == null)
                {
                    // The resolved token contains no Symmetric Security key and thus we cannot create 
                    // a derived key off of it.
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.UnableToDeriveKeyFromKeyInfoClause, keyIdentifierClause, resolvedToken)));
                }

                int derivationLength = (keyIdentifierClause.DerivationLength == 0) ? DerivedKeySecurityToken.DefaultDerivedKeyLength : keyIdentifierClause.DerivationLength;
                if (derivationLength > securityHeader.MaxDerivedKeyLength)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.DerivedKeyLengthSpecifiedInImplicitDerivedKeyClauseTooLong, keyIdentifierClause.ToString(), derivationLength, securityHeader.MaxDerivedKeyLength)));
                }

                bool alreadyDerived = false;
                for (int i = 0; i < tokenCount; ++i)
                {
                    DerivedKeySecurityToken derivedKeyToken = tokens[i].Token as DerivedKeySecurityToken;
                    if (derivedKeyToken != null)
                    {
                        if ((derivedKeyToken.Length == derivationLength) &&
                            (CryptoHelper.IsEqual(derivedKeyToken.Nonce, derivationNonce)) &&
                            (derivedKeyToken.TokenToDerive.MatchesKeyIdentifierClause(keyIdentifierClause)))
                        {
                            // This is a implcit derived key for which we have already derived the
                            // token.
                            resolvedToken = tokens[i].Token;
                            alreadyDerived = true;
                            break;
                        }
                    }
                }

                if (!alreadyDerived)
                {
                    string psha1Algorithm = SecurityUtils.GetKeyDerivationAlgorithm(securityHeader.StandardsManager.MessageSecurityVersion.SecureConversationVersion);

                    resolvedToken = new DerivedKeySecurityToken(-1, 0, derivationLength, null, derivationNonce, resolvedToken, keyIdentifierClause, psha1Algorithm, SecurityUtils.GenerateId());
                    ((DerivedKeySecurityToken)resolvedToken).InitializeDerivedKey(derivationLength);
                    Add(resolvedToken, SecurityTokenReferenceStyle.Internal, null);
                    securityHeader.EnsureDerivedKeyLimitNotReached();
                }
            }

            return resolvedToken;
        }

        public override string ToString()
        {
            using (StringWriter writer = new StringWriter(CultureInfo.InvariantCulture))
            {
                writer.WriteLine("SecurityTokenResolver");
                writer.WriteLine("    (");
                writer.WriteLine("    TokenCount = {0},", tokenCount);
                for (int i = 0; i < tokenCount; i++)
                {
                    writer.WriteLine("    TokenEntry[{0}] = (AllowedReferenceStyle={1}, Token={2}, Parameters={3})",
                        i, tokens[i].AllowedReferenceStyle, tokens[i].Token.GetType(), tokens[i].TokenParameters);
                }
                writer.WriteLine("    )");
                return writer.ToString();
            }
        }

        protected override bool TryResolveTokenCore(SecurityKeyIdentifier keyIdentifier, out SecurityToken token)
        {
            token = ResolveToken(keyIdentifier, false, true);
            return token != null;
        }

        internal bool TryResolveToken(SecurityKeyIdentifier keyIdentifier, bool matchOnlyExternalTokens, bool resolveIntrinsicKeyClause, out SecurityToken token)
        {
            token = ResolveToken(keyIdentifier, matchOnlyExternalTokens, resolveIntrinsicKeyClause);
            return token != null;
        }

        protected override bool TryResolveTokenCore(SecurityKeyIdentifierClause keyIdentifierClause, out SecurityToken token)
        {
            token = ResolveToken(keyIdentifierClause, false, true);
            return token != null;
        }

        internal bool TryResolveToken(SecurityKeyIdentifierClause keyIdentifierClause, bool matchOnlyExternalTokens, bool resolveIntrinsicKeyClause, out SecurityToken token)
        {
            token = ResolveToken(keyIdentifierClause, matchOnlyExternalTokens, resolveIntrinsicKeyClause);
            return token != null;
        }

        internal bool TryResolveSecurityKey(SecurityKeyIdentifierClause keyIdentifierClause, bool createIntrinsicKeys, out SecurityKey key)
        {
            if (keyIdentifierClause == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("keyIdentifierClause");
            }
            key = ResolveSecurityKeyCore(keyIdentifierClause, createIntrinsicKeys);
            return key != null;
        }

        protected override bool TryResolveSecurityKeyCore(SecurityKeyIdentifierClause keyIdentifierClause, out SecurityKey key)
        {
            key = ResolveSecurityKeyCore(keyIdentifierClause, true);
            return key != null;
        }

        private struct SecurityTokenEntry
        {
            private readonly SecurityTokenParameters tokenParameters;
            private readonly SecurityToken token;
            private readonly SecurityTokenReferenceStyle allowedReferenceStyle;

            public SecurityTokenEntry(SecurityToken token, SecurityTokenParameters tokenParameters, SecurityTokenReferenceStyle allowedReferenceStyle)
            {
                this.token = token;
                this.tokenParameters = tokenParameters;
                this.allowedReferenceStyle = allowedReferenceStyle;
            }

            public SecurityToken Token => token;

            public SecurityTokenParameters TokenParameters => tokenParameters;

            public SecurityTokenReferenceStyle AllowedReferenceStyle => allowedReferenceStyle;
        }
    }
}
