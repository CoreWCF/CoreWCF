// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using CoreWCF.IdentityModel.Claims;
using CoreWCF.IdentityModel.Policy;

namespace Helpers
{
    internal class CustomAuthorizationPolicy : IAuthorizationPolicy
    {
        private List<Claim> _claimsToAdd;

        public CustomAuthorizationPolicy(params Claim[] claims)
        {
            _claimsToAdd = claims.ToList();
        }

        public ClaimSet Issuer => ClaimSet.System;

        public string Id { get; } = Guid.NewGuid().ToString();

        public bool Evaluate(EvaluationContext evaluationContext, ref object state)
        {
            bool result = false;
            CustomAuthState customState = null;

            // If the state is null, then this has not been called before so
            // set up a custom state.
            if (state == null)
            {
                customState = new CustomAuthState();
                state = customState;
            }
            else
            {
                customState = (CustomAuthState)state;
            }

            // If claims have not been added yet...
            if (!customState.ClaimsAdded)
            {
                // Add claims to the evaluation context.
                evaluationContext.AddClaimSet(this, new DefaultClaimSet(Issuer, _claimsToAdd));

                // Record that claims were added.
                customState.ClaimsAdded = true;

                // Return true, indicating that this method does not need to be called again.
                result = true;
            }

            return result;
        }

        // Internal class for keeping track of state.
        private class CustomAuthState
        {
            public CustomAuthState()
            {
                ClaimsAdded = false;
            }

            public bool ClaimsAdded { get; set; }
        }
    }
}
