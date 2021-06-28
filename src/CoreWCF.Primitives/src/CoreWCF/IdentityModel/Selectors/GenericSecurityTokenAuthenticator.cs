// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CoreWCF.IdentityModel.Claims;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Security;

namespace CoreWCF.IdentityModel.Selectors
{
    internal class GenericSecurityTokenAuthenticator : SecurityTokenAuthenticator
    {
        private readonly LdapSettings _ldapSettings;
        public GenericSecurityTokenAuthenticator()
        {
        }

        public GenericSecurityTokenAuthenticator(LdapSettings ldapSettings)
        {
            _ldapSettings = ldapSettings;
        }

        protected override bool CanValidateTokenCore(SecurityToken token)
        {
            return token is GenericSecurityToken;
        }

        protected override ReadOnlyCollection<IAuthorizationPolicy> ValidateTokenCore(SecurityToken token)
        {
            var genericToken = (GenericSecurityToken)token;
            string principalName = genericToken.Name;
            if (string.IsNullOrEmpty(principalName))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(principalName));
            }

            Claim identityClaim;
            Claim primaryPrincipal;
            if (principalName.Contains("@") || principalName.Contains(@"\"))
            {
                identityClaim = new Claim(ClaimTypes.Upn, principalName, Rights.Identity);
                primaryPrincipal = Claim.CreateUpnClaim(principalName);
            }
            else
            {
                identityClaim = new Claim(ClaimTypes.Spn, principalName, Rights.Identity);
                primaryPrincipal = Claim.CreateSpnClaim(principalName);
            }

            List<Claim> claims = new List<Claim>(2)
            {
                identityClaim,
                primaryPrincipal
            };


            if (_ldapSettings != null)
            {
                List<Claim> allCaims = LdapAdapter.RetrieveClaimsAsync(_ldapSettings, genericToken.GenericIdentity.Name).Result;
                // if this is made async, many other API changes has to happen. COnsidering this is one of the scenario, ok to take the hit ?
                foreach (Claim claim in allCaims)
                {
                    claims.Add(claim);
                }
            }
            List<IAuthorizationPolicy> policies = new List<IAuthorizationPolicy>(1)
            {
                new UnconditionalPolicy(genericToken.GenericIdentity, new DefaultClaimSet(ClaimSet.Anonymous, claims))
            };
            return policies.AsReadOnly();
        }
    }
}
