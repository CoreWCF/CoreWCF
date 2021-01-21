// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using CoreWCF.IdentityModel.Claims;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Tokens;

namespace CoreWCF.IdentityModel.Selectors
{
    internal class RsaSecurityTokenAuthenticator : SecurityTokenAuthenticator
    {
        public RsaSecurityTokenAuthenticator()
        {
        }

        protected override bool CanValidateTokenCore(SecurityToken token)
        {
            return token is RsaSecurityToken;
        }

        protected override ReadOnlyCollection<IAuthorizationPolicy> ValidateTokenCore(SecurityToken token)
        {
            RsaSecurityToken rsaToken = (RsaSecurityToken)token;
            List<Claim> claims = new List<Claim>(2);
            claims.Add(new Claim(ClaimTypes.Rsa, rsaToken.Rsa, Rights.Identity));
            claims.Add(Claim.CreateRsaClaim(rsaToken.Rsa));

            DefaultClaimSet claimSet = new DefaultClaimSet(ClaimSet.Anonymous, claims);
            List<IAuthorizationPolicy> policies = new List<IAuthorizationPolicy>(1);
            policies.Add(new UnconditionalPolicy(claimSet, rsaToken.ValidTo));
            return policies.AsReadOnly();
        }
    }
}
