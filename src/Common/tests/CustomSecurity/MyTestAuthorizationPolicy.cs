using CoreWCF.IdentityModel.Claims;
using CoreWCF.IdentityModel.Policy;
using System;
using System.Collections.Generic;
using System.Text;

namespace CoreWCF.Primitives.Tests.CustomSecurity
{
    public class MyTestAuthorizationPolicy : IAuthorizationPolicy
    {
        string id;
        public MyTestAuthorizationPolicy()
        {
            id = Guid.NewGuid().ToString();
        }

        public bool Evaluate(EvaluationContext evaluationContext, ref object state)
        {
                bool bRet = false;
            CustomAuthState customstate = null;

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

            // If claims have not been added yet...
            if (!customstate.ClaimsAdded)
            { 
                    // Create an empty list of claims.
                IList<Claim> claims = new List<Claim>();
                claims.Add(new Claim("http://tempuri.org/claims/allowedoperation", "http://tempuri.org/IEchoService/EchoString", Rights.PossessProperty));
                claims.Add(new Claim("http://tempuri.org/claims/allowedoperation", "http://tempuri.org/IEchoService/ComplexEcho", Rights.PossessProperty));
                evaluationContext.AddClaimSet(this, new DefaultClaimSet(this.Issuer, claims));
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

        public string Id
        {
            get { return id; }
        }

        // Internal class for keeping track of state.
        class CustomAuthState
        {
            bool bClaimsAdded;

            public CustomAuthState()
            {
                bClaimsAdded = false;
            }

            public bool ClaimsAdded
            {
                get { return bClaimsAdded; }
                set { bClaimsAdded = value; }
            }
        }
    }
}
