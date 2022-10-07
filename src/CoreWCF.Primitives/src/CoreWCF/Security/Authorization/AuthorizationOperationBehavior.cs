// // Licensed to the .NET Foundation under one or more agreements.
// // The .NET Foundation licenses this file to you under the MIT license.
//
// using System;
// using System.Threading.Tasks;
// using CoreWCF.Channels;
// using CoreWCF.Description;
// using CoreWCF.Dispatcher;
// using Microsoft.AspNetCore.Authorization;
//
// namespace CoreWCF.Security.Authorization
// {
//     public sealed class AuthorizationOperationBehavior : IOperationBehavior
//     {
//         private readonly IAuthorizeData[] _authorizeData;
//         private readonly IAuthorizationPolicyProvider _authorizationPolicyProvider;
//         private readonly IAuthorizationService _authorizationService;
//         private readonly bool _isAllowAnonymous;
//         private readonly Task<AuthorizationPolicy> _getPolicyTask;
//
//         public AuthorizationOperationBehavior(IAuthorizeData[] authorizeData,
//             IAuthorizationPolicyProvider authorizationPolicyProvider,
//             IAuthorizationService authorizationService,
//             bool isAllowAnonymous)
//         {
//             _authorizeData = authorizeData;
//             _authorizationPolicyProvider = authorizationPolicyProvider;
//             _authorizationService = authorizationService;
//             _isAllowAnonymous = isAllowAnonymous;
//
//             _getPolicyTask = _authorizeData.Length == 0
//                 ? _authorizationPolicyProvider.GetDefaultPolicyAsync()
//                 : ComputePolicyAsync();
//         }
//
//         public void Validate(OperationDescription operationDescription)
//         {
//         }
//
//         public void ApplyDispatchBehavior(OperationDescription operationDescription,
//             DispatchOperation dispatchOperation)
//         {
//             dispatchOperation.Invoker = new AuthorizationOperationInvoker(dispatchOperation.Invoker,
//                 _authorizationService, _getPolicyTask, _isAllowAnonymous);
//         }
//
//         public void ApplyClientBehavior(OperationDescription operationDescription, ClientOperation clientOperation)
//         {
//         }
//
//         public void AddBindingParameters(OperationDescription operationDescription,
//             BindingParameterCollection bindingParameters)
//         {
//         }
//
//         private async Task<AuthorizationPolicy> ComputePolicyAsync()
//         {
//             if (_authorizationPolicyProvider == null)
//             {
//                 throw new InvalidOperationException();
//             }
//
//             return (await AuthorizationPolicy.CombineAsync(_authorizationPolicyProvider, _authorizeData!))!;
//         }
//     }
// }
