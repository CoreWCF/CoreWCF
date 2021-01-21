// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using CoreWCF.IdentityModel.Claims;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Security;

namespace CoreWCF.IdentityModel.Selectors
{
    public class WindowsSecurityTokenAuthenticator : SecurityTokenAuthenticator
    {
        private readonly bool includeWindowsGroups;

        public WindowsSecurityTokenAuthenticator() : this(WindowsClaimSet.DefaultIncludeWindowsGroups)
        {
        }

        public WindowsSecurityTokenAuthenticator(bool includeWindowsGroups)
        {
            this.includeWindowsGroups = includeWindowsGroups;
        }

        protected override bool CanValidateTokenCore(SecurityToken token)
        {
            return token is WindowsSecurityToken;
        }

        protected override ReadOnlyCollection<IAuthorizationPolicy> ValidateTokenCore(SecurityToken token)
        {
            var windowsToken = (WindowsSecurityToken)token;
            var claimSet = new WindowsClaimSet(windowsToken.WindowsIdentity, windowsToken.AuthenticationType, includeWindowsGroups, windowsToken.ValidTo);
            return SecurityUtils.CreateAuthorizationPolicies(claimSet, windowsToken.ValidTo);
        }
    }
}
