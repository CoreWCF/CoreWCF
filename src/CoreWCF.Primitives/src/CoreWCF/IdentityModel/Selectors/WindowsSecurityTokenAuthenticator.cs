using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using CoreWCF.IdentityModel.Claims;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Tokens;

namespace CoreWCF.IdentityModel.Selectors
{
    public class WindowsSecurityTokenAuthenticator : SecurityTokenAuthenticator
    {
        bool includeWindowsGroups;

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
            var claimSet = new WindowsClaimSet(windowsToken.WindowsIdentity, windowsToken.AuthenticationType, this.includeWindowsGroups, windowsToken.ValidTo);
            return SecurityUtils.CreateAuthorizationPolicies(claimSet, windowsToken.ValidTo);
        }
    }
}
