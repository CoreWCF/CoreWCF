// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using CoreWCF.IdentityModel.Claims;

namespace CoreWCF.Primitives.Tests.CustomSecurity
{
    public abstract class MyTestServiceAuthorizationManagerBase : ServiceAuthorizationManager
    {
        protected Func<OperationContext, bool> Logic = (OperationContext operationContext) =>
        {
            // Extract the action URI from the OperationContext. Match this against the claims
            // in the AuthorizationContext.
            string action = operationContext.RequestContext.RequestMessage.Headers.Action;
            // Iterate through the various claim sets in the AuthorizationContext.
            bool isWIndowIdentity = false;
            foreach (ClaimSet cs in operationContext.ServiceSecurityContext.AuthorizationContext.ClaimSets)
            {
                // Examine only those claim sets issued by System.
                if (cs.Issuer == ClaimSet.System)
                {
                    foreach (Claim c in cs.FindClaims("http://tempuri.org/claims/allowedoperation",
                                 Rights.PossessProperty))
                    {
                        // If the Claim resource matches the action URI then return true to allow access.
                        if (action == c.Resource.ToString())
                        {
                            return true;
                        }
                    }
                }
                else if (cs.Issuer == ClaimSet.Windows)
                {
                    isWIndowIdentity = true; // unconditionally for windows
                }
            }

            // If this point is reached, return false to deny access.
            return isWIndowIdentity || false;
        };
    }

    public class MySyncTestServiceAuthorizationManager : MyTestServiceAuthorizationManagerBase
    {
        [Obsolete("Implementers should override CheckAccessCoreAsync.")]
        protected override bool CheckAccessCore(OperationContext operationContext) => Logic(operationContext);
    }

    public class MyAsyncTestServiceAuthorizationManager : MyTestServiceAuthorizationManagerBase
    {
        protected override ValueTask<bool> CheckAccessCoreAsync(OperationContext operationContext) => new(Logic(operationContext));
    }
}
