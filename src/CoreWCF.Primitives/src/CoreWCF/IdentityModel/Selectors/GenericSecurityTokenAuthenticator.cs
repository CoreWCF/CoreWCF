// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Tokens;

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
