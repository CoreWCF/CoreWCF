using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CoreWCF.IdentityModel.Claims;

namespace CoreWCF.IdentityModel.Policy
{
    internal abstract class AuthorizationContext : IAuthorizationComponent
    {
        public abstract string Id { get; }
        public abstract ReadOnlyCollection<ClaimSet> ClaimSets { get; }
        public abstract DateTime ExpirationTime { get; }
        public abstract IDictionary<string, object> Properties { get; }

        public static AuthorizationContext CreateDefaultAuthorizationContext(IList<IAuthorizationPolicy> authorizationPolicies)
        {
            //return SecurityUtils.CreateDefaultAuthorizationContext(authorizationPolicies);
            throw new PlatformNotSupportedException();
        }
    }

}