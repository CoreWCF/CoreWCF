// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using System.Security.Claims;
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

        public WindowsSecurityTokenAuthenticator() : this(WindowsClaimSet.DefaultIncludeWindowsGroups, null)
        {
        }

        public WindowsSecurityTokenAuthenticator(bool includeWindowsGroups, LdapSettings ldapSettings)
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
            if (token is WindowsSecurityToken)
            {
                var windowsToken = (WindowsSecurityToken)token;
                var claimSet = new WindowsClaimSet(windowsToken.WindowsIdentity, windowsToken.AuthenticationType, _includeWindowsGroups, windowsToken.ValidTo);
                return SecurityUtils.CreateAuthorizationPolicies(claimSet, windowsToken.ValidTo);
            }
            else
            {
                var genericToken = (GenericSecurityToken)token;
                var claimSet = new WindowsClaimSet((ClaimsIdentity)genericToken.GenericIdentity,  _includeWindowsGroups, _ldapSettings);
                return SecurityUtils.CreateAuthorizationPolicies(claimSet);
            }
        }
    }
}
