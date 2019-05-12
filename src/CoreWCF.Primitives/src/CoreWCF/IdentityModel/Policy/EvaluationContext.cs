using CoreWCF.IdentityModel.Claims;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace CoreWCF.IdentityModel.Policy
{
    internal abstract class EvaluationContext
    {
        public abstract ReadOnlyCollection<ClaimSet> ClaimSets { get; }
        public abstract IDictionary<string, object> Properties { get; }
        public abstract int Generation { get; }
        public abstract void AddClaimSet(IAuthorizationPolicy policy, ClaimSet claimSet);
        public abstract void RecordExpirationTime(DateTime expirationTime);
    }
}
