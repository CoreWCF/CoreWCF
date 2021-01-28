// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Security.Principal;

namespace CoreWCF.IdentityModel.Claims
{
    [DataContract(Namespace = XsiConstants.Namespace)]
    public abstract class ClaimSet : IEnumerable<Claim>
    {
        private static ClaimSet s_system;
        private static ClaimSet s_windows;
        private static ClaimSet s_anonymous;

        public static ClaimSet System
        {
            get
            {
                if (s_system == null)
                {
                    List<Claim> claims = new List<Claim>(2);
                    claims.Add(Claim.System);
                    claims.Add(new Claim(ClaimTypes.System, XsiConstants.System, Rights.PossessProperty));
                    s_system = new DefaultClaimSet(claims);
                }
                return s_system;
            }
        }

        public static ClaimSet Windows
        {
            get
            {
                if (s_windows == null)
                {
                    List<Claim> claims = new List<Claim>(2);
                    SecurityIdentifier sid = new SecurityIdentifier(WellKnownSidType.NTAuthoritySid, null);
                    claims.Add(new Claim(ClaimTypes.Sid, sid, Rights.Identity));
                    claims.Add(Claim.CreateWindowsSidClaim(sid));
                    s_windows = new DefaultClaimSet(claims);
                }
                return s_windows;
            }
        }

        internal static ClaimSet Anonymous
        {
            get
            {
                if (s_anonymous == null)
                {
                    s_anonymous = new DefaultClaimSet();
                }

                return s_anonymous;
            }
        }

        static internal bool SupportedRight(string right)
        {
            return right == null ||
                Rights.Identity.Equals(right) ||
                Rights.PossessProperty.Equals(right);
        }


        public virtual bool ContainsClaim(Claim claim, IEqualityComparer<Claim> comparer)
        {
            if (claim == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(claim));
            }

            if (comparer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(comparer));
            }

            IEnumerable<Claim> claims = FindClaims(null, null);
            if (claims != null)
            {
                foreach (Claim matchingClaim in claims)
                {
                    if (comparer.Equals(claim, matchingClaim))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public virtual bool ContainsClaim(Claim claim)
        {
            if (claim == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(claim));
            }

            IEnumerable<Claim> claims = FindClaims(claim.ClaimType, claim.Right);
            if (claims != null)
            {
                foreach (Claim matchingClaim in claims)
                {
                    if (claim.Equals(matchingClaim))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public abstract Claim this[int index] { get; }
        public abstract int Count { get; }
        public abstract ClaimSet Issuer { get; }
        // Note: null string represents any.
        public abstract IEnumerable<Claim> FindClaims(string claimType, string right);
        public abstract IEnumerator<Claim> GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
    }
}