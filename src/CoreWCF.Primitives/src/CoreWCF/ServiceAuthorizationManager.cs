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
            // delegate to the async CheckAccessAsync(OperationContext operationContext)
            // to give a chance to users who are no longer overriding sync method but th async one to get their code called
            ValueTask<bool> checkAccessAsyncResult = CheckAccessAsync(operationContext);
            return checkAccessAsyncResult.IsCompleted
                ? checkAccessAsyncResult.Result
                : checkAccessAsyncResult.GetAwaiter().GetResult();
        }


        public virtual ValueTask<(bool isAuthorized, Message message)> CheckAccessAsync(OperationContext operationContext, Message message)
        {
            // delegate to matching sync call overload
            bool checkAccessResult = CheckAccess(operationContext, ref message);
            return new ValueTask<(bool isAuthorized, Message message)>((checkAccessResult, message));
        }

        [Obsolete("Implementers should override CheckAccessAsync.")]
        public virtual bool CheckAccess(OperationContext operationContext)
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

            // 3) Call the CheckAccessCoreAsync(OperationContext operationContext)
            // to give a chance to users who are no longer overriding sync method but th async one to get their code called
            ValueTask<bool> checkAccessCoreAsyncResult = CheckAccessCoreAsync(operationContext);
            return checkAccessCoreAsyncResult.IsCompleted
                ? checkAccessCoreAsyncResult.Result
                : checkAccessCoreAsyncResult.GetAwaiter().GetResult();
        }

        public virtual ValueTask<bool> CheckAccessAsync(OperationContext operationContext) => new(CheckAccess(operationContext));

        // Define the set of policies taking part in chaining.  We will provide
        // the safe default set (primary token + all supporting tokens except token with
        // with SecurityTokenAttachmentMode.Signed + transport token). Implementor
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
        protected virtual bool CheckAccessCore(OperationContext operationContext) => true;

        // Implementor overrides this API to make authoritive decision.
        // The AuthorizationContext in opContext is generally the result from forward chain.
        protected virtual ValueTask<bool> CheckAccessCoreAsync(OperationContext operationContext) => new(CheckAccessCore(operationContext));

    }
}
