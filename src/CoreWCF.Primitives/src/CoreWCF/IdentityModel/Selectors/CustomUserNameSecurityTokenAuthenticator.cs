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
        private UserNamePasswordValidator validator;

        public CustomUserNameSecurityTokenAuthenticator(UserNamePasswordValidator validator)
        {
            if (validator == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(validator));
            this.validator = validator;
        }

        protected override ReadOnlyCollection<IAuthorizationPolicy> ValidateUserNamePasswordCore(string userName, string password)
        {
            validator.Validate(userName, password);
            return SecurityUtils.CreateAuthorizationPolicies(new UserNameClaimSet(userName, validator.GetType().Name));
        }

        private class UserNameClaimSet : DefaultClaimSet, IIdentityInfo
        {
            private IIdentity identity;

            public UserNameClaimSet(string userName, string authType)
            {
                if (userName == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(userName));

                identity = SecurityUtils.CreateIdentity(userName, authType);

                List<Claim> claims = new List<Claim>(2);
                claims.Add(new Claim(ClaimTypes.Name, userName, Rights.Identity));
                claims.Add(Claim.CreateNameClaim(userName));
                Initialize(ClaimSet.System, claims);
            }

            public IIdentity Identity
            {
                get { return identity; }
            }
        }
    }
}
