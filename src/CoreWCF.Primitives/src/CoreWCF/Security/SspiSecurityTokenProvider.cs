// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net;
using System.Security.Principal;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Security
{
    public class SspiSecurityTokenProvider : SecurityTokenProvider
    {
        internal const bool DefaultAllowNtlm = true;
        internal const bool DefaultExtractWindowsGroupClaims = true;
        internal const bool DefaultAllowUnauthenticatedCallers = false;
        private readonly SspiSecurityToken token;

        // client side ctor
        public SspiSecurityTokenProvider(NetworkCredential credential, bool allowNtlm, TokenImpersonationLevel impersonationLevel)
        {
            token = new SspiSecurityToken(impersonationLevel, allowNtlm, credential);
        }

        // service side ctor
        public SspiSecurityTokenProvider(NetworkCredential credential, bool extractGroupsForWindowsAccounts, bool allowUnauthenticatedCallers)
        {
            token = new SspiSecurityToken(credential, extractGroupsForWindowsAccounts, allowUnauthenticatedCallers);
        }

        protected override SecurityToken GetTokenCore(TimeSpan timeout)
        {
            return token;
        }
    }
}
