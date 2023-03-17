// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Security.Claims;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.Description;
using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Claims;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Security.Claims;
using SysAuthorizationContext = CoreWCF.IdentityModel.Policy.AuthorizationContext;

namespace CoreWCF.Security
{
    /// <summary>
    /// Custom ServiceAuthorizationManager implementation. This class substitues the WCF
    /// generated IAuthorizationPolicies with
    /// <see cref="CoreWCF.IdentityModel.Tokens.AuthorizationPolicy"/>. These
    /// policies do not participate in the EvaluationContext and hence will render an
    /// empty WCF AuthorizationConext. Once this AuthorizationManager is substitued to
    /// a ServiceHost, only <see cref="System.Security.Claims.ClaimsPrincipal"/>
    /// will be available for Authorization decisions.
    /// </summary>
    internal class IdentityModelServiceAuthorizationManager : ServiceAuthorizationManager
    {
        /// <summary>
        /// Authorization policy for anonymous authentication.
        /// </summary>
        protected static readonly ReadOnlyCollection<IAuthorizationPolicy> AnonymousAuthorizationPolicy
            = new ReadOnlyCollection<IAuthorizationPolicy>(
                new List<IAuthorizationPolicy>() { new AuthorizationPolicy(new ClaimsIdentity()) });

        /// <summary>
        /// Override of the base class method. Substitues WCF IAuthorizationPolicy with
        /// <see cref="CoreWCF.IdentityModel.Tokens.AuthorizationPolicy"/>.
        /// </summary>
        /// <param name="operationContext">Current OperationContext that contains all the IAuthorizationPolicies.</param>
        /// <returns>Read-Only collection of <see cref="IAuthorizationPolicy"/> </returns>
        protected override ReadOnlyCollection<IAuthorizationPolicy> GetAuthorizationPolicies(OperationContext operationContext)
        {
            //
            // Make sure we always return at least one claims identity, if there are no auth policies
            // that contain any identities, then return an anonymous identity wrapped in an authorization policy.
            //
            // If we do not, then Thread.CurrentPrincipal may end up being null inside service operations after the
            // authorization polices are evaluated since ServiceCredentials.ConfigureServiceHost will
            // turn the PrincipalPermissionMode knob to Custom.
            //

            ReadOnlyCollection<IAuthorizationPolicy> baseAuthorizationPolicies = base.GetAuthorizationPolicies(operationContext);
            if (baseAuthorizationPolicies == null)
            {
                return AnonymousAuthorizationPolicy;
            }
            else
            {
                ServiceCredentials sc = GetServiceCredentials();
                AuthorizationPolicy transformedPolicy = TransformAuthorizationPolicies(baseAuthorizationPolicies,
                                                                                        sc.IdentityConfiguration.SecurityTokenHandlers,
                                                                                        true);
                if (transformedPolicy == null || transformedPolicy.IdentityCollection.Count == 0)
                {
                    return AnonymousAuthorizationPolicy;
                }
                return (new List<IAuthorizationPolicy>() { transformedPolicy }).AsReadOnly();
            }
        }

        internal static AuthorizationPolicy TransformAuthorizationPolicies(
            ReadOnlyCollection<IAuthorizationPolicy> baseAuthorizationPolicies,
            SecurityTokenHandlerCollection securityTokenHandlerCollection,
            bool includeTransportTokens)
        {
            List<ClaimsIdentity> identities = new List<ClaimsIdentity>();
            List<IAuthorizationPolicy> uncheckedAuthorizationPolicies = new List<IAuthorizationPolicy>();

            //
            // STEP 1: Filter out the IAuthorizationPolicy that WCF generated. These
            //         are generated as IDFx does not have a proper SecurityTokenHandler
            //         to handle these. For example, SSPI at message layer and all token
            //         types at the Transport layer.
            //
            foreach (IAuthorizationPolicy authPolicy in baseAuthorizationPolicies)
            {
                if ((authPolicy is SctAuthorizationPolicy) ||
                    (authPolicy is EndpointAuthorizationPolicy))
                {
                    //
                    // We ignore the SctAuthorizationPolicy if any found as they were created
                    // as wrapper policies to hold the primary identity claim during a token renewal path.
                    // WCF would otherwise fault thinking the token issuance and renewal identities are
                    // different. This policy should be treated as a dummy policy and thereby should not be transformed.
                    //
                    // We ignore EndpointAuthorizationPolicy as well. This policy is used only to carry
                    // the endpoint Identity and there is no useful claims that this policy contributes.
                    //
                    continue;
                }


                if (authPolicy is AuthorizationPolicy idfxAuthPolicy)
                {
                    // Identities obtained from the Tokens in the message layer would
                    identities.AddRange(idfxAuthPolicy.IdentityCollection);
                }
                else
                {
                    uncheckedAuthorizationPolicies.Add(authPolicy);
                }
            }

            //
            // STEP 2: Generate IDFx claims from the transport token
            //
            if (includeTransportTokens && (OperationContext.Current != null) &&
                (OperationContext.Current.IncomingMessageProperties != null) &&
                (OperationContext.Current.IncomingMessageProperties.Security != null) &&
                (OperationContext.Current.IncomingMessageProperties.Security.TransportToken != null))
            {
                SecurityToken transportToken =
                    OperationContext.Current.IncomingMessageProperties.Security.TransportToken.SecurityToken;

                ReadOnlyCollection<IAuthorizationPolicy> policyCollection =
                    OperationContext.Current.IncomingMessageProperties.Security.TransportToken.SecurityTokenPolicies;
                bool isWcfAuthPolicy = true;

                foreach (IAuthorizationPolicy policy in policyCollection)
                {
                    //
                    // Iterate over each of the policies in the policyCollection to make sure
                    // we don't have an idfx policy, if we do we will not consider this as
                    // a wcf auth policy: Such a case will be hit for the SslStreamSecurityBinding over net tcp
                    //

                    if (policy is AuthorizationPolicy)
                    {
                        isWcfAuthPolicy = false;
                        break;
                    }
                }

                if (isWcfAuthPolicy)
                {
                    ReadOnlyCollection<ClaimsIdentity> tranportTokenIdentities = GetTransportTokenIdentities(transportToken);
                    identities.AddRange(tranportTokenIdentities);

                    //
                    // NOTE: In the below code, we are trying to identify the IAuthorizationPolicy that WCF
                    // created for the Transport token and eliminate it. This assumes that any client Security
                    // Token that came in the Security header would have been validated by the SecurityTokenHandler
                    // and hence would have created a IDFx AuthorizationPolicy.
                    // For example, if X.509 Certificate was used to authenticate the client at the transport layer
                    // and then again at the Message security layer we depend on our TokenHandlers to have been in
                    // place to validate the X.509 Certificate at the message layer. This would clearly distinguish
                    // which policy was created for the Transport token by WCF.
                    //
                    EliminateTransportTokenPolicy(transportToken, tranportTokenIdentities, uncheckedAuthorizationPolicies);
                }
            }

            //
            // STEP 3: Process any uncheckedAuthorizationPolicies here. Convert these to IDFx
            //         Claims.
            //
            if (uncheckedAuthorizationPolicies.Count > 0)
            {
                identities.AddRange(ConvertToIDFxIdentities(uncheckedAuthorizationPolicies, securityTokenHandlerCollection));
            }

            //
            // STEP 4: Create an AuthorizationPolicy with all the ClaimsIdentities.
            //
            AuthorizationPolicy idfxAuthorizationPolicy = null;
            if (identities.Count == 0)
            {
                //
                // No IDFx ClaimsIdentity was found. Return AnonymousIdentity.
                //
                idfxAuthorizationPolicy = new AuthorizationPolicy(new ClaimsIdentity());
            }
            else
            {
                idfxAuthorizationPolicy = new AuthorizationPolicy(identities.AsReadOnly());
            }

            return idfxAuthorizationPolicy;
        }

        /// <summary>
        /// Creates ClaimsIdentityCollection for the given Transport SecurityToken.
        /// </summary>
        /// <param name="transportToken">Client SecurityToken provided at the Transport layer.</param>
        /// <returns>ClaimsIdentityCollection built from the Transport SecurityToken</returns>
        private static ReadOnlyCollection<ClaimsIdentity> GetTransportTokenIdentities(SecurityToken transportToken)
        {
            if (transportToken == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(transportToken));
            }

            ServiceCredentials serviceCreds = GetServiceCredentials();

            List<ClaimsIdentity> transportTokenIdentityCollection = new List<ClaimsIdentity>();

            //////////////////////////////////////////////////////////////////////////////////////////
            //
            // There are 5 well-known Client Authentication types at the transport layer. Each of these will
            // result either in a WindowsSecurityToken, X509SecurityToken or UserNameSecurityToken.
            // All other type of credentials (like OAuth token) result other token that will be passed trough regular validation process.
            //
            //      ClientCredential Type     ||        Transport Token Type
            // -------------------------------------------------------------------
            //          Basic                 ->        UserNameSecurityToken (In Self-hosted case)
            //          Basic                 ->        WindowsSecurityToken (In Web-Hosted case)
            //          NTLM                  ->        WindowsSecurityToken
            //          Negotiate             ->        WindowsSecurityToken
            //          Windows               ->        WindowsSecurityToken
            //          Certificate           ->        X509SecurityToken
            //
            //////////////////////////////////////////////////////////////////////////////////////////

            if (transportToken is WindowsSecurityToken windowsSecurityToken)
            {
                WindowsIdentity claimsIdentity = new WindowsIdentity(windowsSecurityToken.WindowsIdentity.Token,
                    AuthenticationTypes.Windows);
                AddAuthenticationMethod(claimsIdentity, AuthenticationMethods.Windows);
                AddAuthenticationInstantClaim(claimsIdentity, XmlConvert.ToString(DateTime.UtcNow, DateTimeFormats.Generated));

                // Just reflect on the wrapped WindowsIdentity and build the WindowsClaimsIdentity class.
                transportTokenIdentityCollection.Add(claimsIdentity);
            }
            else
            {
                // WCF does not call our SecurityTokenHandlers for the Transport token. So run the token through
                // the SecurityTokenHandler and generate claims for this token.
                transportTokenIdentityCollection.AddRange(serviceCreds.IdentityConfiguration.SecurityTokenHandlers.ValidateToken(transportToken));
            }

            return transportTokenIdentityCollection.AsReadOnly();
        }

        /// <summary>
        /// Given a collection of IAuthorizationPolicies this method will eliminate the IAuthorizationPolicy
        /// that was created for the given transport Security Token. The method modifies the given collection
        /// of IAuthorizationPolicy.
        /// </summary>
        /// <param name="transportToken">Client's Security Token provided at the transport layer.</param>
        /// <param name="tranportTokenIdentities"></param>
        /// <param name="baseAuthorizationPolicies">Collection of IAuthorizationPolicies that were created by WCF.</param>
        private static void EliminateTransportTokenPolicy(
            SecurityToken transportToken,
            IEnumerable<ClaimsIdentity> tranportTokenIdentities,
            List<IAuthorizationPolicy> baseAuthorizationPolicies)
        {
            if (transportToken == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(transportToken));
            }

            if (tranportTokenIdentities == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(tranportTokenIdentities));
            }

            if (baseAuthorizationPolicies == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(baseAuthorizationPolicies));
            }

            if (baseAuthorizationPolicies.Count == 0)
            {
                // This should never happen in our current configuration. IDFx token handlers do not validate
                // client tokens present at the transport level. So we should atleast have one IAuthorizationPolicy
                // that WCF generated for the transport token.
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(baseAuthorizationPolicies), SR.Format(SR.ID0020));
            }

            //
            // We will process one IAuthorizationPolicy at a time. Transport token will have been authenticated
            // by WCF and would have created a IAuthorizationPolicy for the same. If the transport token is a X.509
            // SecurityToken and 'mapToWindows' was set to true then the IAuthorizationPolicy that was created
            // by WCF will have two Claimsets, a X509ClaimSet and a WindowsClaimSet. We need to prune out this case
            // and ignore both these Claimsets as we have made a call to the token handler to authenticate this
            // token above. If we create a AuthorizationContext using all the IAuthorizationPolicies then all
            // the claimsets are merged and it becomes hard to identify this case.
            //
            IAuthorizationPolicy policyToEliminate = null;
            foreach (IAuthorizationPolicy authPolicy in baseAuthorizationPolicies)
            {
                if (DoesPolicyMatchTransportToken(transportToken, tranportTokenIdentities, authPolicy))
                {
                    policyToEliminate = authPolicy;
                    break;
                }
            }

            if (policyToEliminate == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperInvalidOperation(SR.Format(SR.ID4271, transportToken));
            }

            baseAuthorizationPolicies.Remove(policyToEliminate);
        }

        /// <summary>
        /// Returns true if the IAuthorizationPolicy could have been created from the given Transport token.
        /// The method can handle only X509SecurityToken and WindowsSecurityToken.
        /// </summary>
        /// <param name="transportToken">Client's Security Token provided at the transport layer.</param>
        /// <param name="tranportTokenIdentities">A collection of <see cref="ClaimsIdentity"/> to match.</param>
        /// <param name="authPolicy">IAuthorizationPolicy to check.</param>
        /// <returns>True if the IAuthorizationPolicy could have been created from the given Transpor token.</returns>
        private static bool DoesPolicyMatchTransportToken(
            SecurityToken transportToken,
            IEnumerable<ClaimsIdentity> tranportTokenIdentities,
            IAuthorizationPolicy authPolicy
            )
        {
            if (transportToken == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(transportToken));
            }

            if (tranportTokenIdentities == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(tranportTokenIdentities));
            }

            if (authPolicy == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(authPolicy));
            }

            //////////////////////////////////////////////////////////////////////////////////////////
            //
            // There are 5 Client Authentication types at the transport layer. Each of these will
            // result either in a WindowsSecurityToken, X509SecurityToken or UserNameSecurityToken.
            //
            //      ClientCredential Type     ||        Transport Token Type
            // -------------------------------------------------------------------
            //          Basic                 ->        UserNameSecurityToken (In Self-hosted case)
            //          Basic                 ->        WindowsSecurityToken (In Web-Hosted case)
            //          NTLM                  ->        WindowsSecurityToken
            //          Negotiate             ->        WindowsSecurityToken
            //          Windows               ->        WindowsSecurityToken
            //          Certificate           ->        X509SecurityToken
            //
            //////////////////////////////////////////////////////////////////////////////////////////

            SysAuthorizationContext defaultAuthContext = SysAuthorizationContext.CreateDefaultAuthorizationContext(new List<IAuthorizationPolicy>() { authPolicy });

            foreach (CoreWCF.IdentityModel.Claims.ClaimSet claimset in defaultAuthContext.ClaimSets)
            {
                if (transportToken is X509SecurityToken x509SecurityToken)
                {
                    // Check if the claimset contains a claim that matches the X.509 certificate thumbprint.
                    if (claimset.ContainsClaim(new CoreWCF.IdentityModel.Claims.Claim(
                            CoreWCF.IdentityModel.Claims.ClaimTypes.Thumbprint,
                            x509SecurityToken.Certificate.GetCertHash(),
                            CoreWCF.IdentityModel.Claims.Rights.PossessProperty)))
                    {
                        return true;
                    }
                }
                else
                {
                    // For WindowsSecurityToken and UserNameSecurityToken check that IClaimsdentity.Name
                    // matches the Name Claim in the ClaimSet.
                    // In most cases, we will have only one Identity in the ClaimsIdentityCollection
                    // generated from transport token.
                    foreach (ClaimsIdentity transportTokenIdentity in tranportTokenIdentities)
                    {
                        if (claimset.ContainsClaim(new CoreWCF.IdentityModel.Claims.Claim(
                                CoreWCF.IdentityModel.Claims.ClaimTypes.Name,
                                transportTokenIdentity.Name,
                                CoreWCF.IdentityModel.Claims.Rights.PossessProperty), new ClaimStringValueComparer()))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Converts a given set of WCF IAuthorizationPolicy to WIF ClaimIdentities.
        /// </summary>
        /// <param name="authorizationPolicies">Set of AuthorizationPolicies to convert to IDFx.</param>
        /// <param name="securityTokenHandlerCollection">The SecurityTokenHandlerCollection to use.</param>
        /// <returns>ClaimsIdentityCollection</returns>
        private static ReadOnlyCollection<ClaimsIdentity> ConvertToIDFxIdentities(IList<IAuthorizationPolicy> authorizationPolicies,
                                                                 SecurityTokenHandlerCollection securityTokenHandlerCollection)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets the ServiceCredentials from the OperationContext.
        /// </summary>
        /// <returns>ServiceCredentials</returns>
        private static ServiceCredentials GetServiceCredentials()
        {
            ServiceCredentials serviceCredentials = null;

            if (OperationContext.Current != null &&
                OperationContext.Current.Host != null &&
                OperationContext.Current.Host.Description != null &&
                OperationContext.Current.Host.Description.Behaviors != null)
            {
                serviceCredentials = OperationContext.Current.Host.Description.Behaviors.Find<ServiceCredentials>();
            }

            return serviceCredentials;
        }

        // Adds an Authentication Method claims to the given ClaimsIdentity if one is not already present.
        private static void AddAuthenticationMethod(ClaimsIdentity claimsIdentity, string authenticationMethod)
        {
            System.Security.Claims.Claim authenticationMethodClaim =
                        claimsIdentity.Claims.FirstOrDefault(claim => claim.Type == System.Security.Claims.ClaimTypes.AuthenticationMethod);

            if (authenticationMethodClaim == null)
            {
                // AuthenticationMethod claims does not exist. Add one.
                claimsIdentity.AddClaim(
                    new System.Security.Claims.Claim(
                        System.Security.Claims.ClaimTypes.AuthenticationMethod, authenticationMethod));
            }
        }

        // Adds an Authentication Method claims to the given ClaimsIdentity if one is not already present.
        private static void AddAuthenticationInstantClaim(ClaimsIdentity claimsIdentity, string authenticationInstant)
        {
            // the issuer for this claim should always be the default issuer.
            string issuerName = ClaimsIdentity.DefaultIssuer;
            System.Security.Claims.Claim authenticationInstantClaim =
                    claimsIdentity.Claims.FirstOrDefault(claim => claim.Type == System.Security.Claims.ClaimTypes.AuthenticationInstant);

            if (authenticationInstantClaim == null)
            {
                // AuthenticationInstance claims does not exist. Add one.
                claimsIdentity.AddClaim(
                    new System.Security.Claims.Claim(
                        System.Security.Claims.ClaimTypes.AuthenticationInstant, authenticationInstant, ClaimValueTypes.DateTime,
                        issuerName));
            }
        }

        // When a token creates more than one Identity we have to merge these identities.
        // The below method takes two Identities and will return a single identity. If one of the
        // Identities is a WindowsIdentity then all claims from the other identity are
        // merged into the WindowsIdentity. If neither are WindowsIdentity then it
        // selects 'identity1' and merges all the claims from 'identity2' into 'identity1'.
        //
        // It is not clear how we can handler duplicate name claim types and delegates.
        // So, we are just cloning the claims from one identity and adding it to another.
        internal static ClaimsIdentity MergeClaims(ClaimsIdentity identity1, ClaimsIdentity identity2)
        {
            if ((identity1 == null) && (identity2 == null))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperInvalidOperation(SR.Format(SR.ID4268));
            }

            if (identity1 == null)
            {
                return identity2;
            }

            if (identity2 == null)
            {
                return identity1;
            }

            if (identity1 is WindowsIdentity windowsIdentity)
            {
                windowsIdentity.AddClaims(identity2.Claims);
                return windowsIdentity;
            }

            windowsIdentity = identity2 as WindowsIdentity;
            if (windowsIdentity != null)
            {
                windowsIdentity.AddClaims(identity1.Claims);
                return windowsIdentity;
            }

            identity1.AddClaims(identity2.Claims);

            return identity1;
        }

        /// <summary>
        /// Checks authorization for the given operation context based on policy evaluation.
        /// </summary>
        /// <param name="operationContext">The OperationContext for the current authorization request.</param>
        /// <returns>true if authorized, false otherwise</returns>
        protected override ValueTask<bool> CheckAccessCoreAsync(OperationContext operationContext)
        {
            throw new NotImplementedException();
        }
    }

    internal class ClaimStringValueComparer : IEqualityComparer<CoreWCF.IdentityModel.Claims.Claim>
    {
        #region IEqualityComparer<CoreWCF.IdentityModel.Claims.Claim> Members

        public bool Equals(CoreWCF.IdentityModel.Claims.Claim claim1, CoreWCF.IdentityModel.Claims.Claim claim2)
        {
            if (ReferenceEquals(claim1, claim2))
            {
                return true;
            }

            if (claim1 == null || claim2 == null)
            {
                return false;
            }

            if (claim1.ClaimType != claim2.ClaimType || claim1.Right != claim2.Right)
            {
                return false;
            }

            return StringComparer.OrdinalIgnoreCase.Equals(claim1.Resource, claim2.Resource);
        }

        public int GetHashCode(CoreWCF.IdentityModel.Claims.Claim claim)
        {
            if (claim == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(claim));
            }

            return claim.ClaimType.GetHashCode() ^ claim.Right.GetHashCode()
                ^ ((claim.Resource == null) ? 0 : claim.Resource.GetHashCode());
        }

        #endregion
    }

    internal class SecurityTokenSpecificationEnumerable : IEnumerable<SecurityTokenSpecification>
    {
        private readonly SecurityMessageProperty _securityMessageProperty;

        public SecurityTokenSpecificationEnumerable(SecurityMessageProperty securityMessageProperty)
        {
            _securityMessageProperty = securityMessageProperty ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(securityMessageProperty));
        }

        public IEnumerator<SecurityTokenSpecification> GetEnumerator()
        {
            if (_securityMessageProperty.InitiatorToken != null)
            {
                yield return _securityMessageProperty.InitiatorToken;
            }

            if (_securityMessageProperty.ProtectionToken != null)
            {
                yield return _securityMessageProperty.ProtectionToken;
            }

            if (_securityMessageProperty.HasIncomingSupportingTokens)
            {
                foreach (SecurityTokenSpecification tokenSpecification in _securityMessageProperty.IncomingSupportingTokens)
                {
                    if (tokenSpecification != null)
                    {
                        yield return tokenSpecification;
                    }
                }
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotImplementedException());
        }

    }

}
