// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using CoreWCF.Channels;
using CoreWCF.Dispatcher;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace CoreWCF.Security.Authorization
{
    internal class AuthorizationOperationInvoker : IOperationInvoker
    {
        private readonly IOperationInvoker _operationInvoker;
        private readonly IAuthorizationService _authorizationService;
        private readonly Task<AuthorizationPolicy> _getPolicyTask;
        private readonly bool _isAllowAnonymous;

        public AuthorizationOperationInvoker(IOperationInvoker operationInvoker,
            IAuthorizationService authorizationService, Task<AuthorizationPolicy> getPolicyTask, bool isAllowAnonymous)
        {
            _operationInvoker = operationInvoker;
            _authorizationService = authorizationService;
            _getPolicyTask = getPolicyTask;
            _isAllowAnonymous = isAllowAnonymous;
        }

        public object[] AllocateInputs() => _operationInvoker.AllocateInputs();

        public async ValueTask<(object returnValue, object[] outputs)> InvokeAsync(object instance, object[] inputs)
        {
            var httpContext = OperationContext.Current.IncomingMessageProperties["Microsoft.AspNetCore.Http.HttpContext"] as HttpContext;
            string authenticationScheme = httpContext!.Items["CoreWCF.Channels.HttpTransportSettings.CustomAuthenticationScheme"] as string;
            var principal = httpContext.User;

            if (!principal.Identity.IsAuthenticated)
            {
                if (!_isAllowAnonymous)
                {
                    return (new AuthenticationErrorMessage(authenticationScheme), Array.Empty<object>());
                }

                return await _operationInvoker.InvokeAsync(instance, inputs);
            }

            var policy = await _getPolicyTask;
            var authorizationResult = await _authorizationService.AuthorizeAsync(principal, policy);
            if (!authorizationResult.Succeeded)
            {
                return (new AuthorizationErrorMessage(authenticationScheme), Array.Empty<object>());
            }

            return await _operationInvoker.InvokeAsync(instance, inputs);
        }
    }
}
