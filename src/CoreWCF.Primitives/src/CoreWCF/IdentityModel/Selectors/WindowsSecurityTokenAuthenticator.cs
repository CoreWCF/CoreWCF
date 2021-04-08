// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using CoreWCF.IdentityModel.Claims;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Security;

namespace CoreWCF.IdentityModel.Selectors
{
    public class WindowsSecurityTokenAuthenticator : SecurityTokenAuthenticator
    {
        private readonly bool _includeWindowsGroups;
        private readonly LdapSettings _ldapSettings;

        public WindowsSecurityTokenAuthenticator() : this(WindowsClaimSet.DefaultIncludeWindowsGroups)
        {
        }

        public WindowsSecurityTokenAuthenticator(bool includeWindowsGroups) : this(includeWindowsGroups,  null)
        {
        }

        public WindowsSecurityTokenAuthenticator(bool includeWindowsGroups,LdapSettings ldapSettings)
        {
            _includeWindowsGroups = includeWindowsGroups;
            _ldapSettings = ldapSettings;
        }

        protected override bool CanValidateTokenCore(SecurityToken token)
        {
            return token is WindowsSecurityToken;
        }

        protected override ReadOnlyCollection<IAuthorizationPolicy> ValidateTokenCore(SecurityToken token)
        {
            var windowsToken = (WindowsSecurityToken)token;
            var claimSet = new WindowsClaimSet(windowsToken.WindowsIdentity, windowsToken.AuthenticationType, _includeWindowsGroups, windowsToken.ValidTo);
            if (_ldapSettings != null && claimSet.Count > 0)
            {
                List<Claim> ldapClaims = LdapAdapter.RetrieveClaims(_ldapSettings, windowsToken.WindowsIdentity.Name);
                foreach (Claim claim in ldapClaims)
                {
                    claimSet.AddClaim(claim);
                }
            }
            return SecurityUtils.CreateAuthorizationPolicies(claimSet, windowsToken.ValidTo);
        }
    }
}
