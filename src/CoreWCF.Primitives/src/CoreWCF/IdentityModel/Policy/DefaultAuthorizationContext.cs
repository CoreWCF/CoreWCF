using CoreWCF.IdentityModel.Claims;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace CoreWCF.IdentityModel.Policy
{
   internal class DefaultAuthorizationContext : AuthorizationContext
    {
        SecurityUniqueId id;
        ReadOnlyCollection<ClaimSet> claimSets;
        DateTime expirationTime;
        IDictionary<string, object> properties;

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
