// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CoreWCF.IdentityModel.Claims;
using CoreWCF.Security;

namespace CoreWCF.IdentityModel.Policy
{
    internal class DefaultEvaluationContext : EvaluationContext
    {
        private List<ClaimSet> claimSets;
        private readonly Dictionary<string, object> properties;
        private int generation;
        private ReadOnlyCollection<ClaimSet> readOnlyClaimSets;

        public DefaultEvaluationContext()
        {
            properties = new Dictionary<string, object>();
            generation = 0;
        }

        public override int Generation
        {
            get { return generation; }
        }

        public override ReadOnlyCollection<ClaimSet> ClaimSets
        {
            get
            {
                if (claimSets == null)
                {
                    return EmptyReadOnlyCollection<ClaimSet>.Instance;
                }

                if (readOnlyClaimSets == null)
                {
                    readOnlyClaimSets = new ReadOnlyCollection<ClaimSet>(claimSets);
                }

                return readOnlyClaimSets;
            }
        }

        public override IDictionary<string, object> Properties
        {
            get { return properties; }
        }

        public DateTime ExpirationTime { get; private set; } = SecurityUtils.MaxUtcDateTime;

        public override void AddClaimSet(IAuthorizationPolicy policy, ClaimSet claimSet)
        {
            if (claimSet == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("claimSet");
            }

            if (claimSets == null)
            {
                claimSets = new List<ClaimSet>();
            }

            claimSets.Add(claimSet);
            ++generation;
        }

        public override void RecordExpirationTime(DateTime expirationTime)
        {
            if (ExpirationTime > expirationTime)
            {
                ExpirationTime = expirationTime;
            }
        }
    }
}
