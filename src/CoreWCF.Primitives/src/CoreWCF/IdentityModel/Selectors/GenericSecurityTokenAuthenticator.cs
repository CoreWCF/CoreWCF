using System;
using System.Collections.ObjectModel;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Security;

namespace CoreWCF.IdentityModel.Selectors
{
    public class GenericSecurityTokenAuthenticator : SecurityTokenAuthenticator
    {
        public GenericSecurityTokenAuthenticator()
        {
        }

        protected override bool CanValidateTokenCore(SecurityToken token)
        {
            return token is GenericSecurityToken;
        }

        protected override ReadOnlyCollection<IAuthorizationPolicy> ValidateTokenCore(SecurityToken token)
        {
            var genericToken = (GenericSecurityToken)token;
            return Security.SecurityUtils.CreatePrincipalNameAuthorizationPolicies(genericToken.ServicePrincipalName);
        }
    }
}
