using CoreWCF.IdentityModel.Claims;
using CoreWCF.IdentityModel.Policy;
using CoreWCF;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Principal;
using System.Text;

namespace CoreWCF.IdentityModel.Selectors
{
    internal class CustomUserNameSecurityTokenAuthenticator : UserNameSecurityTokenAuthenticator
    {
        UserNamePasswordValidator validator;

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

        class UserNameClaimSet : DefaultClaimSet, IIdentityInfo
        {
            IIdentity identity;

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
