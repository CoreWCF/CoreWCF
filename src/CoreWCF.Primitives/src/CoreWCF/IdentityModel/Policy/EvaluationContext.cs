// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CoreWCF.IdentityModel.Claims;

namespace CoreWCF.IdentityModel.Policy
{
    public abstract class EvaluationContext
    {
        public abstract ReadOnlyCollection<ClaimSet> ClaimSets { get; }
        public abstract IDictionary<string, object> Properties { get; }
        public abstract int Generation { get; }
        public abstract void AddClaimSet(IAuthorizationPolicy policy, ClaimSet claimSet);
        public abstract void RecordExpirationTime(DateTime expirationTime);
    }
}
