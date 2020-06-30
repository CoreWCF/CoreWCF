using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace CoreWCF.IdentityModel.Selectors
{
    class GenericSecurityTokenAuthenticator : SecurityTokenAuthenticator
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
            return CoreWCF.Security.SecurityUtils.CreatePrincipalNameAuthorizationPolicies(genericToken.Name);
        }
    }
}
