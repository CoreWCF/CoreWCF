// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.Runtime;
using CoreWCF.Security;

namespace CoreWCF.Dispatcher
{
    internal sealed class AuthorizationBehavior
    {
        private static readonly ServiceAuthorizationManager DefaultServiceAuthorizationManager = new ServiceAuthorizationManager();
        private ReadOnlyCollection<IAuthorizationPolicy> externalAuthorizationPolicies;
        private ServiceAuthorizationManager serviceAuthorizationManager;

        private AuthorizationBehavior() { }

        public void Authorize(ref MessageRpc rpc)
        {
            // TODO: Events 
            SecurityMessageProperty security = SecurityMessageProperty.GetOrCreate(rpc.Request);
            security.ExternalAuthorizationPolicies = externalAuthorizationPolicies;

            ServiceAuthorizationManager serviceAuthorizationManager = this.serviceAuthorizationManager ?? DefaultServiceAuthorizationManager;
            try
            {
                if (!serviceAuthorizationManager.CheckAccess(rpc.OperationContext, ref rpc.Request))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateAccessDeniedFaultException());
                }
            }
            catch (Exception ex)
            {
                // I know this code looks weird and it looks like the try/catch block should be removed, but I'm maintaining
                // this code structure in preparation for Perf counters and auditing to be put back in.
                if (Fx.IsFatal(ex))
                {
                    throw;
                }
                // TODO: PerformanceCounters
                // TODO: Auditing
                throw;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static AuthorizationBehavior CreateAuthorizationBehavior(DispatchRuntime dispatch)
        {
            AuthorizationBehavior behavior = new AuthorizationBehavior();
            behavior.externalAuthorizationPolicies = dispatch.ExternalAuthorizationPolicies;
            behavior.serviceAuthorizationManager = dispatch.ServiceAuthorizationManager;
            return behavior;
        }

        public static AuthorizationBehavior TryCreate(DispatchRuntime dispatch)
        {
            if (dispatch == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(dispatch)));
            }

            if (!dispatch.RequiresAuthorization)
            {
                return null;
            }

            return CreateAuthorizationBehavior(dispatch);
        }

        internal static Exception CreateAccessDeniedFaultException()
        {
            SecurityVersion wss = SecurityVersion.Default;
            FaultCode faultCode = FaultCode.CreateSenderFaultCode(wss.FailedAuthenticationFaultCode.Value, wss.HeaderNamespace.Value);
            FaultReason faultReason = new FaultReason(SR.AccessDenied, CultureInfo.CurrentCulture);
            return new FaultException(faultReason, faultCode);
        }
    }
}