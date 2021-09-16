// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.IdentityModel.Claims;
using CoreWCF.IdentityModel.Policy;

namespace CoreWCF.IdentityModel.Tokens
{
    /// <summary>
    /// Implementation of IAuthorizationPolicy that contains endpoint specific 
    /// AuthorizationPolicy.
    /// </summary>
    internal class EndpointAuthorizationPolicy : IAuthorizationPolicy
    {
        private readonly string _id = IdentityModelUniqueId.CreateUniqueId();

        /// <summary>
        /// Creates an instance of <see cref="EndpointAuthorizationPolicy"/>
        /// </summary>
        /// <param name="endpointId">Identifier of the Endpoint to which the token should be restricted.</param>
        public EndpointAuthorizationPolicy( string endpointId )
        {
            EndpointId = endpointId ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(endpointId));
        }

        /// <summary>
        /// Gets the EndpointId for the AuthorizationPolicy
        /// </summary>
        public string EndpointId { get; }

        #region IAuthorizationPolicy Members

        /// <summary>
        /// Check if the claims in the EvaluationContext.
        /// </summary>
        /// <param name="evaluationContext">The current evaluationContext</param>
        /// <param name="state">Any custom state.</param>
        /// <returns>Returns true by default.</returns>
        bool IAuthorizationPolicy.Evaluate(EvaluationContext evaluationContext, ref object state) => true;

        /// <summary>
        /// Gets the Issuer ClaimSet. Returns null by default.
        /// </summary>
        ClaimSet IAuthorizationPolicy.Issuer => null;

        #endregion

        #region IAuthorizationComponent Members

        /// <summary>
        /// Gets the Id.
        /// </summary>
        string IAuthorizationComponent.Id => _id;
        #endregion
    }

}
