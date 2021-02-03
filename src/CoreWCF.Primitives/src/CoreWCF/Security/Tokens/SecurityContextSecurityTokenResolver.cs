// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using System.Xml;
using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;

namespace CoreWCF.Security.Tokens
{
    public class SecurityContextSecurityTokenResolver : SecurityTokenResolver, ISecurityContextSecurityTokenCache
    {
        private readonly SecurityContextTokenCache _tokenCache;
        private TimeSpan _clockSkew = SecurityProtocolFactory.defaultMaxClockSkew;

        public SecurityContextSecurityTokenResolver(int securityContextCacheCapacity, bool removeOldestTokensOnCacheFull)
            : this(securityContextCacheCapacity, removeOldestTokensOnCacheFull, SecurityProtocolFactory.defaultMaxClockSkew)
        {
        }

        public SecurityContextSecurityTokenResolver(int securityContextCacheCapacity, bool removeOldestTokensOnCacheFull, TimeSpan clockSkew)
        {
            if (securityContextCacheCapacity <= 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(securityContextCacheCapacity), SR.ValueMustBeGreaterThanZero));
            }

            if (clockSkew < TimeSpan.Zero)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(clockSkew), SR.TimeSpanCannotBeLessThanTimeSpanZero));
            }

            SecurityContextTokenCacheCapacity = securityContextCacheCapacity;
            RemoveOldestTokensOnCacheFull = removeOldestTokensOnCacheFull;
            _clockSkew = clockSkew;
            _tokenCache = new SecurityContextTokenCache(SecurityContextTokenCacheCapacity, RemoveOldestTokensOnCacheFull, clockSkew);
        }

        public int SecurityContextTokenCacheCapacity { get; }

        public TimeSpan ClockSkew
        {
            get
            {
                return _clockSkew;
            }
        }

        public bool RemoveOldestTokensOnCacheFull { get; }

        public void AddContext(SecurityContextSecurityToken token)
        {
            _tokenCache.AddContext(token);
        }

        public bool TryAddContext(SecurityContextSecurityToken token)
        {
            return _tokenCache.TryAddContext(token);
        }


        public void ClearContexts()
        {
            _tokenCache.ClearContexts();
        }

        public void RemoveContext(UniqueId contextId, UniqueId generation)
        {
            _tokenCache.RemoveContext(contextId, generation, false);
        }

        public void RemoveAllContexts(UniqueId contextId)
        {
            _tokenCache.RemoveAllContexts(contextId);
        }

        public SecurityContextSecurityToken GetContext(UniqueId contextId, UniqueId generation)
        {
            return _tokenCache.GetContext(contextId, generation);
        }

        public Collection<SecurityContextSecurityToken> GetAllContexts(UniqueId contextId)
        {
            return _tokenCache.GetAllContexts(contextId);
        }

        public void UpdateContextCachingTime(SecurityContextSecurityToken context, DateTime expirationTime)
        {
            _tokenCache.UpdateContextCachingTime(context, expirationTime);
        }

        protected override bool TryResolveTokenCore(SecurityKeyIdentifierClause keyIdentifierClause, out SecurityToken token)
        {
            if (keyIdentifierClause is SecurityContextKeyIdentifierClause sctSkiClause)
            {
                token = _tokenCache.GetContext(sctSkiClause.ContextId, sctSkiClause.Generation);
            }
            else
            {
                token = null;
            }
            return (token != null);
        }

        protected override bool TryResolveSecurityKeyCore(SecurityKeyIdentifierClause keyIdentifierClause, out SecurityKey key)
        {
            if (TryResolveTokenCore(keyIdentifierClause, out SecurityToken sct))
            {
                key = ((SecurityContextSecurityToken)sct).SecurityKeys[0];
                return true;
            }
            else
            {
                key = null;
                return false;
            }
        }

        protected override bool TryResolveTokenCore(SecurityKeyIdentifier keyIdentifier, out SecurityToken token)
        {
            if (keyIdentifier.TryFind<SecurityContextKeyIdentifierClause>(out SecurityContextKeyIdentifierClause sctSkiClause))
            {
                return TryResolveToken(sctSkiClause, out token);
            }
            else
            {
                token = null;
                return false;
            }
        }
    }
}
