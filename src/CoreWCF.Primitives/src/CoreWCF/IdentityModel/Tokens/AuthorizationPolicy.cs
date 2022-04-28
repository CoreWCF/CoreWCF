// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Claims;
using System.Security.Principal;
using CoreWCF.IdentityModel.Policy;
using SysClaimSet = CoreWCF.IdentityModel.Claims.ClaimSet;

namespace CoreWCF.IdentityModel.Tokens
{
    /// <summary>
    /// Defines an AuthorizationPolicy that carries the IDFx Claims. When IDFx is enabled 
    /// a new set of Security Token Authenticators are added to the system. These Authenticators 
    /// will generate the new Claims defined in System.Security.Claims.
    /// </summary>
    internal class AuthorizationPolicy : IAuthorizationPolicy
    {
        public const string ClaimsPrincipalKey = "ClaimsPrincipal"; // This key must be different from "Principal". "Principal" is reserved for Custom mode.
        public const string IdentitiesKey = "Identities";
        private readonly List<ClaimsIdentity> _identityCollection = new List<ClaimsIdentity>();

        /// <summary>
        /// Initializes an instance of <see cref="AuthorizationPolicy"/>
        /// </summary>
        public AuthorizationPolicy()
        {
        }

        /// <summary>
        /// Initializes an instance of <see cref="AuthorizationPolicy"/>
        /// </summary>
        /// <param name="identity">ClaimsIdentity for the AuthorizationPolicy.</param>
        /// <exception cref="ArgumentNullException">One of the input argument is null.</exception>
        public AuthorizationPolicy(ClaimsIdentity identity)
        {
            if (identity == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(identity));
            }

            _identityCollection.Add(identity);
        }

        /// <summary>
        /// Initializes an instance of <see cref="AuthorizationPolicy"/>
        /// </summary>
        /// <param name="identityCollection">Collection of identities.</param>
        public AuthorizationPolicy(IEnumerable<ClaimsIdentity> identityCollection)
        {
            if (identityCollection == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(identityCollection));
            }

            List<ClaimsIdentity> collection = new List<ClaimsIdentity>();
            foreach (ClaimsIdentity identity in identityCollection)
            {
                collection.Add(identity);
            }

            _identityCollection = collection;
        }

        /// <summary>
        /// Gets a ClaimsIdentity collection.
        /// </summary>
        public ReadOnlyCollection<ClaimsIdentity> IdentityCollection
        {
            get
            {
                return _identityCollection.AsReadOnly();
            }
        }

        #region IAuthorizationPolicy Members

        /// <summary>
        /// Evaluates the current Policy. This is provided for backward compatibility
        /// of WCF Claims model. We always return true without affecting the EvaluationContext.
        /// </summary>
        /// <param name="evaluationContext">The current EvaluationContext.</param>
        /// <param name="state">The reference state object.</param>
        /// <returns>True if the Policy was successfully applied.</returns>
        public bool Evaluate(EvaluationContext evaluationContext, ref object state)
        {
            if (null == evaluationContext || null == evaluationContext.Properties)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(evaluationContext));
            }

            if (0 == _identityCollection.Count)
            {
                //
                // Nothing to do here.
                //
                return true;
            }

            //
            // Locate or create the ClaimsPrincipal
            //
            if (!evaluationContext.Properties.TryGetValue(ClaimsPrincipalKey, out object principalObj))
            {
                ClaimsPrincipal principalToAdd = CreateClaimsPrincipalFromIdentities(_identityCollection);

                evaluationContext.Properties.Add(ClaimsPrincipalKey, principalToAdd);
            }
            else
            {
                if (principalObj is ClaimsPrincipal principal && null != principal.Identities)
                {
                    principal.AddIdentities(_identityCollection);
                }
                else
                {
                }
            }

            //
            // Locate or create evaluationContext.Properties[ "Identities" ] with identities
            //
            if (!evaluationContext.Properties.TryGetValue(IdentitiesKey, out object identitiesObj))
            {
                List<ClaimsIdentity> identities = new List<ClaimsIdentity>();
                foreach (ClaimsIdentity ici in _identityCollection)
                {
                    identities.Add(ici);
                }

                evaluationContext.Properties.Add(IdentitiesKey, identities);
            }
            else
            {
                List<ClaimsIdentity> identities;
                identities = identitiesObj as List<ClaimsIdentity>;

                foreach (ClaimsIdentity ici in _identityCollection)
                {
                    identities.Add(ici);
                }
            }

            return true;
        }

        private static ClaimsPrincipal CreateClaimsPrincipalFromIdentities(IEnumerable<ClaimsIdentity> identities)
        {
            ClaimsIdentity selectedClaimsIdentity = SelectPrimaryIdentity(identities);

            if (selectedClaimsIdentity == null)
            {
                //return an anonymous identity
                return new ClaimsPrincipal(new ClaimsIdentity());
            }

            ClaimsPrincipal principal = CreateFromIdentity(selectedClaimsIdentity);

            // Add the remaining identities.
            foreach (ClaimsIdentity identity in identities)
            {
                if (identity != selectedClaimsIdentity)
                {
                    principal.AddIdentity(identity);
                }
            }

            return principal;
        }

        /// <summary>
        /// Creates the appropriate implementation of an IClaimsPrincipal base on the
        /// type of the specified IIdentity (e.g. WindowsClaimsPrincipal for a WindowsIdentity). 
        /// Note the appropriate IClaimsIdentity is generated based on the specified IIdentity
        /// as well.
        /// </summary>
        /// <param name="identity">An implementation of IIdentity</param>
        /// <returns>A claims-based principal.</returns>
        private static ClaimsPrincipal CreateFromIdentity(IIdentity identity)
        {
            if (null == identity)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(identity));
            }

            if (identity is WindowsIdentity wci)
            {
                return new WindowsPrincipal(wci);
            }

            if (identity is WindowsIdentity wi)
            {
                return new WindowsPrincipal(wi);
            }

            if (identity is ClaimsIdentity ici)
            {
                return new ClaimsPrincipal(ici);
            }

            return new ClaimsPrincipal(new ClaimsIdentity(identity));
        }

        /// <summary>
        /// This method iterates through the collection of ClaimsIdentities
        /// and determines which identity must be used as the primary one.
        /// </summary>
        /// <remarks>
        /// If the identities collection contains a WindowsClaimsIdentity, it is the most preferred.
        /// If the identities collection contains an RsaClaimsIdentity, it is the least preferred.
        /// </remarks>
        private static ClaimsIdentity SelectPrimaryIdentity(IEnumerable<ClaimsIdentity> identities)
        {
            if (identities == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(identities));
            }

            //
            // Loop through the identities to determine the primary identity.
            //
            ClaimsIdentity selectedClaimsIdentity = null;

            foreach (ClaimsIdentity identity in identities)
            {
                if (identity is WindowsIdentity)
                {
                    //
                    // If there is a WindowsIdentity, return that.
                    //
                    selectedClaimsIdentity = identity;
                    break;
                }
                else if (identity.FindFirst(ClaimTypes.Rsa) != null)
                {
                    //this is a RSA identity
                    //it is the least preffered identity
                    if (selectedClaimsIdentity == null)
                    {
                        selectedClaimsIdentity = identity;
                    }

                    continue;
                }
                else if (selectedClaimsIdentity == null)
                {
                    //
                    // If no primary identity has been selected yet, choose the current identity.
                    //
                    selectedClaimsIdentity = identity;
                }
            }

            return selectedClaimsIdentity;
        }


        /// <summary>
        /// Gets the Issuer Claimset. This will return a DefaultClaimSet with just one claim 
        /// whose ClaimType is http://schemas.microsoft.com/claims/identityclaim.
        /// </summary>
        public SysClaimSet Issuer { get; } = SysClaimSet.System;

        #endregion

        #region IAuthorizationComponent Members

        /// <summary>
        /// Returns an Id for the ClaimsPrincipal.
        /// </summary>
        public string Id { get; } = SecurityUniqueId.Create().Value;

        #endregion
    }

}
