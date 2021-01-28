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
        private List<ClaimSet> _claimSets;
        private readonly Dictionary<string, object> _properties;
        private int _generation;
        private ReadOnlyCollection<ClaimSet> _readOnlyClaimSets;

        public DefaultEvaluationContext()
        {
            _properties = new Dictionary<string, object>();
            _generation = 0;
        }

        public override int Generation
        {
            get { return _generation; }
        }

        public override ReadOnlyCollection<ClaimSet> ClaimSets
        {
            get
            {
                if (_claimSets == null)
                {
                    return EmptyReadOnlyCollection<ClaimSet>.Instance;
                }

                if (_readOnlyClaimSets == null)
                {
                    _readOnlyClaimSets = new ReadOnlyCollection<ClaimSet>(_claimSets);
                }

                return _readOnlyClaimSets;
            }
        }

        public override IDictionary<string, object> Properties
        {
            get { return _properties; }
        }

        public DateTime ExpirationTime { get; private set; } = SecurityUtils.MaxUtcDateTime;

        public override void AddClaimSet(IAuthorizationPolicy policy, ClaimSet claimSet)
        {
            if (claimSet == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("claimSet");
            }

            if (_claimSets == null)
            {
                _claimSets = new List<ClaimSet>();
            }

            _claimSets.Add(claimSet);
            ++_generation;
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
