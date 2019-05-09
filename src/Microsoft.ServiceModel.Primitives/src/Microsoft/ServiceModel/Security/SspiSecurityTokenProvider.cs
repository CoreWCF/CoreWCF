using Microsoft.IdentityModel.Selectors;
using Microsoft.IdentityModel.Tokens;
using Microsoft.ServiceModel.Security.Tokens;
using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Principal;
using System.Text;

namespace Microsoft.ServiceModel.Security
{
    public class SspiSecurityTokenProvider : SecurityTokenProvider
    {
        internal const bool DefaultAllowNtlm = true;
        internal const bool DefaultExtractWindowsGroupClaims = true;
        internal const bool DefaultAllowUnauthenticatedCallers = false;
        SspiSecurityToken token;

        // client side ctor
        public SspiSecurityTokenProvider(NetworkCredential credential, bool allowNtlm, TokenImpersonationLevel impersonationLevel)
        {
            this.token = new SspiSecurityToken(impersonationLevel, allowNtlm, credential);
        }

        // service side ctor
        public SspiSecurityTokenProvider(NetworkCredential credential, bool extractGroupsForWindowsAccounts, bool allowUnauthenticatedCallers)
        {
            this.token = new SspiSecurityToken(credential, extractGroupsForWindowsAccounts, allowUnauthenticatedCallers);
        }

        protected override SecurityToken GetTokenCore(TimeSpan timeout)
        {
            return this.token;
        }
    }
}
