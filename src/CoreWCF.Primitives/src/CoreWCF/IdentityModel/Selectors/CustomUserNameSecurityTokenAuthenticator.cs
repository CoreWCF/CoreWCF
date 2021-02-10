// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Principal;
using CoreWCF.IdentityModel.Claims;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.Security;

namespace CoreWCF.IdentityModel.Selectors
{
    internal class CustomUserNameSecurityTokenAuthenticator : UserNameSecurityTokenAuthenticator
    {
        private readonly UserNamePasswordValidator _validator;

        public CustomUserNameSecurityTokenAuthenticator(UserNamePasswordValidator validator)
        {
            _validator = validator ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(validator));
        }

        protected override ReadOnlyCollection<IAuthorizationPolicy> ValidateUserNamePasswordCore(string userName, string password)
        {
            _validator.Validate(userName, password);
            return SecurityUtils.CreateAuthorizationPolicies(new UserNameClaimSet(userName, _validator.GetType().Name));
        }

        private class UserNameClaimSet : DefaultClaimSet, IIdentityInfo
        {
            public UserNameClaimSet(string userName, string authType)
            {
                if (userName == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(userName));
                }

                Identity = SecurityUtils.CreateIdentity(userName, authType);

                List<Claim> claims = new List<Claim>(2)
                {
                    new Claim(ClaimTypes.Name, userName, Rights.Identity),
                    Claim.CreateNameClaim(userName)
                };
                Initialize(System, claims);
            }

            public IIdentity Identity { get; }
        }
    }
}
