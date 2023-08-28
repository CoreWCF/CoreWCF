﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using CoreWCF.IdentityModel.Claims;
using CoreWCF.IdentityModel.Policy;

namespace CoreWCF.Primitives.Tests.CustomSecurity
{
    public class MyTestAuthorizationPolicy : IAuthorizationPolicy
    {
        public MyTestAuthorizationPolicy()
        {
            Id = Guid.NewGuid().ToString();
        }

        public bool Evaluate(EvaluationContext evaluationContext, ref object state)
        {
            CustomAuthState customstate;

            // If the state is null, then this has not been called before so 
            // set up a custom state.
            if (state == null)
            {
                customstate = new CustomAuthState();
                state = customstate;
            }
            else
            {
                customstate = (CustomAuthState)state;
            }

            bool bRet;
            // If claims have not been added yet...
            if (!customstate.ClaimsAdded)
            {
                // Create an empty list of claims.
                IList<Claim> claims = new List<Claim>
                {
                    new Claim("http://tempuri.org/claims/allowedoperation", "http://tempuri.org/IEchoService/EchoString", Rights.PossessProperty),
                    new Claim("http://tempuri.org/claims/allowedoperation", "http://tempuri.org/IEchoService/ComplexEcho", Rights.PossessProperty)
                };
                evaluationContext.AddClaimSet(this, new DefaultClaimSet(Issuer, claims));
                // Record that claims were added.
                customstate.ClaimsAdded = true;
                // Return true, indicating that this method does not need to be called again.
                bRet = true;
            }
            else
            {
                // Should never get here, but just in case, return true.
                bRet = true;
            }

            return bRet;
        }

        public ClaimSet Issuer
        {
            get { return ClaimSet.System; }
        }

        public string Id { get; private set; }

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
