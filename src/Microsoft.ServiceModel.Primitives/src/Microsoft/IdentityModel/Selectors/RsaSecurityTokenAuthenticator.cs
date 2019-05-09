using Microsoft.IdentityModel.Claims;
using Microsoft.IdentityModel.Policy;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace Microsoft.IdentityModel.Selectors
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
