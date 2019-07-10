using System.Runtime.CompilerServices;
using System.Collections.ObjectModel;
using CoreWCF.Dispatcher;
using CoreWCF.Security;
using CoreWCF.IdentityModel.Policy;
using CoreWCF;
using CoreWCF.Runtime;
using System;

namespace CoreWCF.Dispatcher
{
    sealed class AuthenticationBehavior
    {
        ServiceAuthenticationManager _serviceAuthenticationManager;

        AuthenticationBehavior(ServiceAuthenticationManager authenticationManager)
        {
            _serviceAuthenticationManager = authenticationManager;
        }

        public void Authenticate(ref MessageRpc rpc)
        {
            SecurityMessageProperty security = SecurityMessageProperty.GetOrCreate(rpc.Request);
            ReadOnlyCollection<IAuthorizationPolicy> authPolicy = security.ServiceSecurityContext.AuthorizationPolicies;
            try
            {
                authPolicy = _serviceAuthenticationManager.Authenticate(security.ServiceSecurityContext.AuthorizationPolicies, rpc.Channel.ListenUri, ref rpc.Request);
                if (authPolicy == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.AuthenticationManagerShouldNotReturnNull));
                }
            }
            catch (Exception ex)
            {
                if (Fx.IsFatal(ex))
                {
                    throw;
                }

                // TODO: PerformanceCounters
                // TODO: Decide if we want Auditing and add back
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateFailedAuthenticationFaultException());
            }

            rpc.Request.Properties.Security.ServiceSecurityContext.AuthorizationPolicies = authPolicy;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static AuthenticationBehavior CreateAuthenticationBehavior(DispatchRuntime dispatch)
        {
            AuthenticationBehavior authenticationBehavior = new AuthenticationBehavior(dispatch.ServiceAuthenticationManager);
            return authenticationBehavior;
        }

        public static AuthenticationBehavior TryCreate(DispatchRuntime dispatch)
        {
            if (dispatch == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(dispatch));

            if (!dispatch.RequiresAuthentication)
                return null;

            return CreateAuthenticationBehavior(dispatch);
        }

        internal static Exception CreateFailedAuthenticationFaultException()
        {
            SecurityVersion wss = SecurityVersion.Default;
            FaultCode faultCode = FaultCode.CreateSenderFaultCode(wss.InvalidSecurityFaultCode.Value, wss.HeaderNamespace.Value);
            FaultReason faultReason = new FaultReason(SR.AuthenticationOfClientFailed);
            return new FaultException(faultReason, faultCode);
        }
    }

}
