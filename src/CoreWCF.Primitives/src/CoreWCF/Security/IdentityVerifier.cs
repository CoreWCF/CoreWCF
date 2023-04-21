// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Principal;
using CoreWCF.Channels;
using CoreWCF.IdentityModel.Claims;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.Runtime.Diagnostics;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Security
{
    public abstract class IdentityVerifier
    {
        protected IdentityVerifier()
        {
            // empty
        }

        public static IdentityVerifier CreateDefault()
        {
            return DefaultIdentityVerifier.Instance;
        }

        internal bool CheckAccess(EndpointAddress reference, Message message)
        {
            if (reference == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(reference));
            }

            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));
            }

            if (!TryGetIdentity(reference, out EndpointIdentity identity))
            {
                return false;
            }

            //SecurityMessageProperty securityContextProperty = null;
            //if (message.Properties != null)
            //    securityContextProperty = message.Properties.Security;

            //if (securityContextProperty == null || securityContextProperty.ServiceSecurityContext == null)
            //    return false;

            //return this.CheckAccess(identity, securityContextProperty.ServiceSecurityContext.AuthorizationContext);
            return false;
        }

        public abstract bool CheckAccess(EndpointIdentity identity, AuthorizationContext authContext);

        public abstract bool TryGetIdentity(EndpointAddress reference, out EndpointIdentity identity);

        private static void AdjustAddress(ref EndpointAddress reference, Uri via)
        {
            // if we don't have an identity and we have differing Uris, we should use the Via
            if (reference.Identity == null && reference.Uri != via)
            {
                reference = new EndpointAddress(via);
            }
        }

        internal bool TryGetIdentity(EndpointAddress reference, Uri via, out EndpointIdentity identity)
        {
            AdjustAddress(ref reference, via);
            return TryGetIdentity(reference, out identity);
        }

        internal void EnsureIncomingIdentity(EndpointAddress serviceReference, AuthorizationContext authorizationContext)
        {
            EnsureIdentity(serviceReference, authorizationContext, SR.IdentityCheckFailedForIncomingMessage);
        }

        internal void EnsureOutgoingIdentity(EndpointAddress serviceReference, Uri via, AuthorizationContext authorizationContext)
        {
            AdjustAddress(ref serviceReference, via);
            EnsureIdentity(serviceReference, authorizationContext, SR.IdentityCheckFailedForOutgoingMessage);
        }

        internal void EnsureOutgoingIdentity(EndpointAddress serviceReference, ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies)
        {
            if (authorizationPolicies == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(authorizationPolicies));
            }
            AuthorizationContext ac = AuthorizationContext.CreateDefaultAuthorizationContext(authorizationPolicies);
            EnsureIdentity(serviceReference, ac, SR.IdentityCheckFailedForOutgoingMessage);
        }

        private void EnsureIdentity(EndpointAddress serviceReference, AuthorizationContext authorizationContext, string errorString)
        {
            if (authorizationContext == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(authorizationContext));
            }
            if (!TryGetIdentity(serviceReference, out EndpointIdentity identity))
            {
                //SecurityTraceRecordHelper.TraceIdentityVerificationFailure(identity, authorizationContext, this.GetType());
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(new MessageSecurityException(SR.Format(errorString, identity, serviceReference)));
            }
            else
            {
                if (!CheckAccess(identity, authorizationContext))
                {
                    // CheckAccess performs a Trace on failure, no need to do it twice
                    Exception e = CreateIdentityCheckException(identity, authorizationContext, errorString, serviceReference);
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperWarning(e);
                }
            }
        }

        private Exception CreateIdentityCheckException(EndpointIdentity identity, AuthorizationContext authorizationContext, string errorString, EndpointAddress serviceReference)
        {
            Exception result;

            if (identity.IdentityClaim != null
                && identity.IdentityClaim.ClaimType == ClaimTypes.Dns
                && identity.IdentityClaim.Right == Rights.PossessProperty
                && identity.IdentityClaim.Resource is string)
            {
                string expectedDnsName = (string)identity.IdentityClaim.Resource;
                string actualDnsName = null;
                for (int i = 0; i < authorizationContext.ClaimSets.Count; ++i)
                {
                    ClaimSet claimSet = authorizationContext.ClaimSets[i];
                    foreach (Claim claim in claimSet.FindClaims(ClaimTypes.Dns, Rights.PossessProperty))
                    {
                        if (claim.Resource is string)
                        {
                            actualDnsName = (string)claim.Resource;
                            break;
                        }
                    }
                    if (actualDnsName != null)
                    {
                        break;
                    }
                }
                if (SR.IdentityCheckFailedForIncomingMessage.Equals(errorString))
                {
                    if (actualDnsName == null)
                    {
                        result = new MessageSecurityException(SR.Format(SR.DnsIdentityCheckFailedForIncomingMessageLackOfDnsClaim, expectedDnsName));
                    }
                    else
                    {
                        result = new MessageSecurityException(SR.Format(SR.DnsIdentityCheckFailedForIncomingMessage, expectedDnsName, actualDnsName));
                    }
                }
                else if (SR.IdentityCheckFailedForOutgoingMessage.Equals(errorString))
                {
                    if (actualDnsName == null)
                    {
                        result = new MessageSecurityException(SR.Format(SR.DnsIdentityCheckFailedForOutgoingMessageLackOfDnsClaim, expectedDnsName));
                    }
                    else
                    {
                        result = new MessageSecurityException(SR.Format(SR.DnsIdentityCheckFailedForOutgoingMessage, expectedDnsName, actualDnsName));
                    }
                }
                else
                {
                    result = new MessageSecurityException(SR.Format(errorString, identity, serviceReference));
                }
            }
            else
            {
                result = new MessageSecurityException(SR.Format(errorString, identity, serviceReference));
            }

            return result;
        }

        private class DefaultIdentityVerifier : IdentityVerifier
        {
            public static DefaultIdentityVerifier Instance { get; } = new DefaultIdentityVerifier();

            public override bool TryGetIdentity(EndpointAddress reference, out EndpointIdentity identity)
            {
                if (reference == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(reference));
                }

                identity = reference.Identity;

                if (identity == null)
                {
                    identity = TryCreateDnsIdentity(reference);
                }

                if (identity == null)
                {
                    SecurityTraceRecordHelper.TraceIdentityDeterminationFailure(reference, typeof(DefaultIdentityVerifier));
                    return false;
                }
                else
                {
                    SecurityTraceRecordHelper.TraceIdentityDeterminationSuccess(reference, identity, typeof(DefaultIdentityVerifier));
                    return true;
                }
            }

            private EndpointIdentity TryCreateDnsIdentity(EndpointAddress reference)
            {
                Uri toAddress = reference.Uri;

                if (!toAddress.IsAbsoluteUri)
                {
                    return null;
                }

                return EndpointIdentity.CreateDnsIdentity(toAddress.DnsSafeHost);
            }

            private SecurityIdentifier GetSecurityIdentifier(Claim claim)
            {
                // if the incoming claim is a SID and the EndpointIdentity is UPN/SPN/DNS, try to find the SID corresponding to
                // the UPN/SPN/DNS (transactions case)
                if (claim.Resource is WindowsIdentity)
                {
                    return ((WindowsIdentity)claim.Resource).User;
                }
                else if (claim.Resource is WindowsSidIdentity)
                {
                    return ((WindowsSidIdentity)claim.Resource).SecurityIdentifier;
                }

                return claim.Resource as SecurityIdentifier;
            }

            private Claim CheckDnsEquivalence(ClaimSet claimSet, string expectedSpn)
            {
                // host/<machine-name> satisfies the DNS identity claim
                IEnumerable<Claim> claims = claimSet.FindClaims(ClaimTypes.Spn, Rights.PossessProperty);
                foreach (Claim claim in claims)
                {
                    if (expectedSpn.Equals((string)claim.Resource, StringComparison.OrdinalIgnoreCase))
                    {
                        return claim;
                    }
                }
                return null;
            }

            private Claim CheckSidEquivalence(SecurityIdentifier identitySid, ClaimSet claimSet)
            {
                foreach (Claim claim in claimSet)
                {
                    SecurityIdentifier sid = GetSecurityIdentifier(claim);
                    if (sid != null)
                    {
                        if (identitySid.Equals(sid))
                        {
                            return claim;
                        }
                    }
                }
                return null;
            }

            public override bool CheckAccess(EndpointIdentity identity, AuthorizationContext authContext)
            {
                //EventTraceActivity eventTraceActivity = null;

                if (identity == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(identity));
                }

                if (authContext == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(authContext));
                }


                //if (FxTrace.Trace.IsEnd2EndActivityTracingEnabled)
                //{
                //    eventTraceActivity = EventTraceActivityHelper.TryExtractActivity((OperationContext.Current != null) ? OperationContext.Current.IncomingMessage : null);
                //}

                for (int i = 0; i < authContext.ClaimSets.Count; ++i)
                {
                    ClaimSet claimSet = authContext.ClaimSets[i];
                    if (claimSet.ContainsClaim(identity.IdentityClaim))
                    {
                        //SecurityTraceRecordHelper.TraceIdentityVerificationSuccess(eventTraceActivity, identity, identity.IdentityClaim, this.GetType());
                        return true;
                    }

                    // try Claim equivalence
                    string expectedSpn = null;
                    if (ClaimTypes.Dns.Equals(identity.IdentityClaim.ClaimType))
                    {
                        expectedSpn = string.Format(CultureInfo.InvariantCulture, "host/{0}", (string)identity.IdentityClaim.Resource);
                        Claim claim = CheckDnsEquivalence(claimSet, expectedSpn);
                        if (claim != null)
                        {
                            //SecurityTraceRecordHelper.TraceIdentityVerificationSuccess(eventTraceActivity, identity, claim, this.GetType());
                            return true;
                        }
                    }
                    // Allow a Sid claim to support UPN, and SPN identities
                    SecurityIdentifier identitySid = null;
                    if (ClaimTypes.Sid.Equals(identity.IdentityClaim.ClaimType))
                    {
                        identitySid = GetSecurityIdentifier(identity.IdentityClaim);
                    }
                    else if (ClaimTypes.Upn.Equals(identity.IdentityClaim.ClaimType))
                    {
                        identitySid = ((UpnEndpointIdentity)identity).GetUpnSid();
                    }
                    else if (ClaimTypes.Spn.Equals(identity.IdentityClaim.ClaimType))
                    {
                        identitySid = ((SpnEndpointIdentity)identity).GetSpnSid();
                    }
                    else if (ClaimTypes.Dns.Equals(identity.IdentityClaim.ClaimType))
                    {
                        identitySid = new SpnEndpointIdentity(expectedSpn).GetSpnSid();
                    }
                    if (identitySid != null)
                    {
                        Claim claim = CheckSidEquivalence(identitySid, claimSet);
                        if (claim != null)
                        {
                            //SecurityTraceRecordHelper.TraceIdentityVerificationSuccess(eventTraceActivity, identity, claim, this.GetType());
                            return true;
                        }
                    }
                }
                SecurityTraceRecordHelper.TraceIdentityVerificationFailure(identity, authContext, GetType());
                //if (TD.SecurityIdentityVerificationFailureIsEnabled())
                //{
                //    TD.SecurityIdentityVerificationFailure(eventTraceActivity);
                //}

                return false;
            }
        }
    }
}
