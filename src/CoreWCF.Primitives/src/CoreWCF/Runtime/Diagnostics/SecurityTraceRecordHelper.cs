﻿using System;
using CoreWCF.IdentityModel.Claims;
using CoreWCF.IdentityModel.Policy;
using CoreWCF;
using CoreWCF.Diagnostics;

namespace CoreWCF.Runtime.Diagnostics
{
    internal static class SecurityTraceRecordHelper
    {
        internal static void TraceIdentityDeterminationFailure(EndpointAddress epr, Type identityVerifier)
        {
            //if (DiagnosticUtility.ShouldTraceInformation)
            //    TraceUtility.TraceEvent(TraceEventType.Information, TraceCode.SecurityIdentityDeterminationFailure, SR.Format(SR.TraceCodeSecurityIdentityDeterminationFailure), new IdentityDeterminationFailureTraceRecord(epr, identityVerifier));
        }

        internal static void TraceIdentityDeterminationSuccess(EndpointAddress epr, EndpointIdentity identity, Type identityVerifier)
        {
            //if (DiagnosticUtility.ShouldTraceInformation)
            //    TraceUtility.TraceEvent(TraceEventType.Information, TraceCode.SecurityIdentityDeterminationSuccess, SR.Format(SR.TraceCodeSecurityIdentityDeterminationSuccess), new IdentityDeterminationSuccessTraceRecord(epr, identity, identityVerifier));
        }

        internal static void TraceIdentityVerificationFailure(EndpointIdentity identity, AuthorizationContext authContext, Type identityVerifier)
        {
            //if (DiagnosticUtility.ShouldTraceInformation)
            //    TraceUtility.TraceEvent(TraceEventType.Information, TraceCode.SecurityIdentityVerificationFailure, SR.Format(SR.TraceCodeSecurityIdentityVerificationFailure), new IdentityVerificationFailureTraceRecord(identity, authContext, identityVerifier));
        }
    }
}