// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.IdentityModel.Claims;
using CoreWCF.IdentityModel.Policy;
using SysClaim = CoreWCF.IdentityModel.Claims.Claim;

namespace CoreWCF.IdentityModel.Tokens
{
    /// <summary>
    /// This class serves as a dummy AuthorizationPolicy on an issued token so that when
    /// WCF renews a token it can match the identity of the issuer with the renewer. This is 
    /// required as in the IDFX layer we throw the WCF generated AuthorizationPolicy ( UnconditionalPolicy )
    /// </summary>
    internal class SctAuthorizationPolicy : IAuthorizationPolicy
    {
        private readonly ClaimSet _issuer;
        private readonly string _id = SecurityUniqueId.Create().Value;

        internal SctAuthorizationPolicy( SysClaim claim )
        {
            _issuer = new DefaultClaimSet( claim );
        }

        #region IAuthorizationPolicy Members

        bool IAuthorizationPolicy.Evaluate( EvaluationContext evaluationContext, ref object state )
        {
            if ( evaluationContext == null )
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(evaluationContext));
            }
            evaluationContext.AddClaimSet( this, _issuer );
            return true;
        }

        ClaimSet IAuthorizationPolicy.Issuer
        {
            get
            {
                return _issuer;
            }
        }

        #endregion

        #region IAuthorizationComponent Members

        string IAuthorizationComponent.Id
        {
            get
            {
                return _id;
            }
        }
        #endregion
    }
}
