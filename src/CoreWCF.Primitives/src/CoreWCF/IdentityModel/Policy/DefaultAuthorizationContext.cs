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
        private SecurityUniqueId id;
        private readonly ReadOnlyCollection<ClaimSet> claimSets;
        private readonly DateTime expirationTime;
        private readonly IDictionary<string, object> properties;

        public DefaultAuthorizationContext(DefaultEvaluationContext evaluationContext)
        {
            claimSets = evaluationContext.ClaimSets;
            expirationTime = evaluationContext.ExpirationTime;
            properties = evaluationContext.Properties;
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
                if (id == null)
                {
                    id = SecurityUniqueId.Create();
                }

                return id.Value;
            }
        }

        public override ReadOnlyCollection<ClaimSet> ClaimSets
        {
            get { return claimSets; }
        }


        public override DateTime ExpirationTime
        {
            get { return expirationTime; }
        }

        public override IDictionary<string, object> Properties
        {
            get { return properties; }
        }
    }
}
