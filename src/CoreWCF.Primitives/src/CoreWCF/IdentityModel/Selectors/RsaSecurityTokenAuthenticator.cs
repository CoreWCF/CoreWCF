// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
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

        protected override ValueTask<ReadOnlyCollection<IAuthorizationPolicy>> ValidateTokenCoreAsync(SecurityToken token)
        {
            RsaSecurityToken rsaToken = (RsaSecurityToken)token;
            List<Claim> claims = new List<Claim>(2)
            {
                new Claim(ClaimTypes.Rsa, rsaToken.Rsa, Rights.Identity),
                Claim.CreateRsaClaim(rsaToken.Rsa)
            };

            DefaultClaimSet claimSet = new DefaultClaimSet(ClaimSet.Anonymous, claims);
            List<IAuthorizationPolicy> policies = new List<IAuthorizationPolicy>(1)
            {
                new UnconditionalPolicy(claimSet, rsaToken.ValidTo)
            };
            return new ValueTask<ReadOnlyCollection<IAuthorizationPolicy>>(policies.AsReadOnly());
        }
    }
}
