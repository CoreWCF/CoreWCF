// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using CoreWCF.IdentityModel.Claims;

namespace CoreWCF.IdentityModel.Policy
{
    internal class DefaultAuthorizationContext : AuthorizationContext
    {
        private SecurityUniqueId _id;
        private readonly ReadOnlyCollection<ClaimSet> _claimSets;
        private readonly DateTime _expirationTime;
        private readonly IDictionary<string, object> _properties;

        public DefaultAuthorizationContext(DefaultEvaluationContext evaluationContext)
        {
            _claimSets = evaluationContext.ClaimSets;
            _expirationTime = evaluationContext.ExpirationTime;
            _properties = evaluationContext.Properties;
        }

        public static DefaultAuthorizationContext Empty
        {
            get
            {
                return new DefaultAuthorizationContext(new DefaultEvaluationContext());
            }
        }

        public override string Id
        {
            get
            {
                if (_id == null)
                {
                    _id = SecurityUniqueId.Create();
                }

                return _id.Value;
            }
        }

        public override ReadOnlyCollection<ClaimSet> ClaimSets
        {
            get { return _claimSets; }
        }


        public override DateTime ExpirationTime
        {
            get { return _expirationTime; }
        }

        public override IDictionary<string, object> Properties
        {
            get { return _properties; }
        }
    }
}
