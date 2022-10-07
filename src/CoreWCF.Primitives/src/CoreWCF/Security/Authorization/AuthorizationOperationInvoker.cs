// // Licensed to the .NET Foundation under one or more agreements.
// // The .NET Foundation licenses this file to you under the MIT license.
//
// using System;
// using System.Threading.Tasks;
// using CoreWCF.Channels;
// using CoreWCF.Dispatcher;
// using Microsoft.AspNetCore.Authorization;
// using Microsoft.AspNetCore.Http;
//
// namespace CoreWCF.Security.Authorization
// {
//     internal class AuthorizationOperationInvoker : IOperationInvoker
//     {
//         private readonly IOperationInvoker _operationInvoker;
//         private readonly IAuthorizationService _authorizationService;
//         public Task<AuthorizationPolicy> GetPolicyTask { get; }
//         public bool IsAllowAnonymous { get; }
//
//         public AuthorizationOperationInvoker(IOperationInvoker operationInvoker,
//             IAuthorizationService authorizationService, Task<AuthorizationPolicy> getPolicyTask, bool isAllowAnonymous)
//         {
//             _operationInvoker = operationInvoker;
//             _authorizationService = authorizationService;
//             GetPolicyTask = getPolicyTask;
//             IsAllowAnonymous = isAllowAnonymous;
//         }
//
//         public object[] AllocateInputs() => _operationInvoker.AllocateInputs();
//
//         public async ValueTask<(object returnValue, object[] outputs)> InvokeAsync(object instance, object[] inputs) => await _operationInvoker.InvokeAsync(instance, inputs);
//     }
// }
