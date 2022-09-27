// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CoreWCF.Channels;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.Security;

namespace CoreWCF
{
    public class ServiceAuthorizationManager
    {
        // This is the API called by framework to perform CheckAccess.
        // The API is responsible for ...
        // 1) Evaluate all policies (Forward\Backward)
        // 2) Optionally wire up the resulting AuthorizationContext
        //    to ServiceSecurityContext.
        // 3) An availability of message content to make an authoritive decision.
        // 4) Return the authoritive decision true/false (allow/deny).
        [Obsolete("Implementers should override CheckAccessAsync.")]
        public virtual bool CheckAccess(OperationContext operationContext, ref Message message)
        {
            // We don't call this internally, so we won't have problems with sync over async causing performance issues
            ValueTask<(bool isAuthorized, Message message)> checkAccessResult = CheckAccessAsync(operationContext, message);
            (bool isAuthorized, Message message) result = checkAccessResult.IsCompleted
                ? checkAccessResult.Result
                : checkAccessResult.AsTask().GetAwaiter().GetResult();
            message = result.message;
            return result.isAuthorized;
        }

        public virtual async ValueTask<(bool isAuthorized, Message message)> CheckAccessAsync(OperationContext operationContext, Message message)
        {
            bool checkAccessResult = await CheckAccessAsync(operationContext);
            return (checkAccessResult, message);
        }

        [Obsolete("Implementers should override CheckAccessAsync.")]
        public virtual bool CheckAccess(OperationContext operationContext)
        {
            // We don't call this internally, so we won't have problems with sync over async causing performance issues
            var checkAccessResult = CheckAccessAsync(operationContext);
            return checkAccessResult.IsCompleted
                ? checkAccessResult.Result
                : checkAccessResult.AsTask().GetAwaiter().GetResult();
        }

        public virtual async ValueTask<bool> CheckAccessAsync(OperationContext operationContext)
        {
            if (operationContext == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(operationContext));
            }

            // default to forward-chaining implementation
            // 1) Get policies that will participate in chain process.
            //    We provide a safe default policies set below.
            ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies = GetAuthorizationPolicies(operationContext);

            // 2) Do forward chaining and wire the new ServiceSecurityContext
            operationContext.IncomingMessageProperties.Security.ServiceSecurityContext =
                new ServiceSecurityContext(authorizationPolicies ?? EmptyReadOnlyCollection<IAuthorizationPolicy>.Instance);

            // 3) Call the CheckAccessCore
            return await CheckAccessCoreAsync(operationContext);
        }

        // Define the set of policies taking part in chaining.  We will provide
        // the safe default set (primary token + all supporting tokens except token with
        // with SecurityTokenAttachmentMode.Signed + transport token).  Implementor
        // can override and provide different selection of policies set.
        protected virtual ReadOnlyCollection<IAuthorizationPolicy> GetAuthorizationPolicies(OperationContext operationContext)
        {
            SecurityMessageProperty security = operationContext.IncomingMessageProperties.Security;
            if (security == null)
            {
                return EmptyReadOnlyCollection<IAuthorizationPolicy>.Instance;
            }

            ReadOnlyCollection<IAuthorizationPolicy> externalPolicies = security.ExternalAuthorizationPolicies;
            if (security.ServiceSecurityContext == null)
            {
                return externalPolicies ?? EmptyReadOnlyCollection<IAuthorizationPolicy>.Instance;
            }

            ReadOnlyCollection<IAuthorizationPolicy> authorizationPolicies = security.ServiceSecurityContext.AuthorizationPolicies;
            if (externalPolicies == null || externalPolicies.Count <= 0)
            {
                return authorizationPolicies;
            }

            // Combine
            List<IAuthorizationPolicy> policies = new(authorizationPolicies);
            policies.AddRange(externalPolicies);
            return policies.AsReadOnly();
        }

        [Obsolete("Implementers should override CheckAccessCoreAsync.")]
        protected virtual bool CheckAccessCore(OperationContext operationContext)
        {
            // We don't call this internally, so we won't have problems with sync over async causing performance issues
            var checkAccessCoreResult = CheckAccessCoreAsync(operationContext);
            return checkAccessCoreResult.IsCompleted
                ? checkAccessCoreResult.Result
                : checkAccessCoreResult.AsTask().GetAwaiter().GetResult();
        }

        // Implementor overrides this API to make authoritive decision.
        // The AuthorizationContext in opContext is generally the result from forward chain.
        protected virtual ValueTask<bool> CheckAccessCoreAsync(OperationContext operationContext)
        {
            return new ValueTask<bool>(true);
        }
    }
}
