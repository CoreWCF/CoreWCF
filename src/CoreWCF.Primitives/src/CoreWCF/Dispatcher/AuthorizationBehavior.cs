// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Threading.Tasks;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.Runtime;
using CoreWCF.Security;
using Microsoft.AspNetCore.Authorization;

namespace CoreWCF.Dispatcher
{
    internal sealed class AuthorizationBehavior
    {
        private static readonly ServiceAuthorizationManager s_defaultServiceAuthorizationManager = new ServiceAuthorizationManager();
        private ReadOnlyCollection<IAuthorizationPolicy> _externalAuthorizationPolicies;
        private ServiceAuthorizationManager _serviceAuthorizationManager;
        private IAuthorizationService _authorizationService;

        private AuthorizationBehavior() { }

        public async ValueTask<MessageRpc> AuthorizeAsync(MessageRpc rpc)
        {
            // TODO: Events
            ClaimsPrincipal claimsPrincipal = rpc.OperationContext.ClaimsPrincipal;
            AuthorizationPolicy authorizationPolicy = rpc.Operation.AuthorizationPolicy?.Value;
            if (claimsPrincipal != null && authorizationPolicy != null)
            {
                await AuthorizePolicyAsync(claimsPrincipal, authorizationPolicy);
                return rpc;
            }

            SecurityMessageProperty security = SecurityMessageProperty.GetOrCreate(rpc.Request);
            security.ExternalAuthorizationPolicies = _externalAuthorizationPolicies;

            ServiceAuthorizationManager serviceAuthorizationManager = _serviceAuthorizationManager ?? s_defaultServiceAuthorizationManager;
            try
            {
                var checkAccessResult = await serviceAuthorizationManager.CheckAccessAsync(rpc.OperationContext, rpc.Request);
                rpc.Request = checkAccessResult.message;
                if(!checkAccessResult.isAuthorized)
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

            return rpc;
        }

        private async ValueTask AuthorizePolicyAsync(ClaimsPrincipal principal, AuthorizationPolicy policy)
        {
            var result = await _authorizationService.AuthorizeAsync(principal, policy);
            if (!result.Succeeded)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateAccessDeniedFaultException());
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static AuthorizationBehavior CreateAuthorizationBehavior(DispatchRuntime dispatch)
        {
            AuthorizationBehavior behavior = new AuthorizationBehavior
            {
                _externalAuthorizationPolicies = dispatch.ExternalAuthorizationPolicies,
                _serviceAuthorizationManager = dispatch.ServiceAuthorizationManager,
                _authorizationService = dispatch.AuthorizationService,
            };

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
