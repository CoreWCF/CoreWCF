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
        private ReadOnlyCollection<ClaimSet> claimSets;
        private DateTime expirationTime;
        private IDictionary<string, object> properties;

        public DefaultAuthorizationContext(DefaultEvaluationContext evaluationContext)
        {
            this.claimSets = evaluationContext.ClaimSets;
            this.expirationTime = evaluationContext.ExpirationTime;
            this.properties = evaluationContext.Properties;
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
                if (this.id == null)
                    this.id = SecurityUniqueId.Create();
                return this.id.Value;
            }
        }

        public override ReadOnlyCollection<ClaimSet> ClaimSets
        {
            get { return this.claimSets; }
        }


        public override DateTime ExpirationTime
        {
            get { return this.expirationTime; }
        }

        public override IDictionary<string, object> Properties
        {
            get { return this.properties; }
        }
    }
}
