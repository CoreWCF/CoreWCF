// // Licensed to the .NET Foundation under one or more agreements.
// // The .NET Foundation licenses this file to you under the MIT license.
//
// using System;
// using System.Collections.ObjectModel;
// using System.Linq;
// using System.Reflection;
// using CoreWCF.Channels;
// using CoreWCF.Description;
// using Microsoft.AspNetCore.Authorization;
//
// namespace CoreWCF.Security.Authorization
// {
//     public class AuthorizationServiceBehavior : IServiceBehavior
//     {
//         private readonly IAuthorizationPolicyProvider _authorizationPolicyProvider;
//         private readonly IAuthorizationService _authorizationService;
//
//         public AuthorizationServiceBehavior(IAuthorizationPolicyProvider authorizationPolicyProvider,
//             IAuthorizationService authorizationService)
//         {
//             _authorizationPolicyProvider = authorizationPolicyProvider;
//             _authorizationService = authorizationService;
//         }
//
//         public void Validate(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase)
//         {
//
//         }
//
//         public void AddBindingParameters(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase,
//             Collection<ServiceEndpoint> endpoints,
//             BindingParameterCollection bindingParameters)
//         {
//
//         }
//
//         public void ApplyDispatchBehavior(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase)
//         {
//             foreach (var endpoint in serviceDescription.Endpoints)
//             {
//                 foreach (var operation in endpoint.Contract.Operations)
//                 {
//                     MethodInfo methodInfo = GetServiceImplMethodInfo(serviceDescription, operation);
//                     bool hasAllowAnonymous = methodInfo.GetCustomAttribute(typeof(AllowAnonymousAttribute)) is not null;
//                     var authorizeData = methodInfo
//                         .GetCustomAttributes(typeof(AuthorizeAttribute), false).OfType<IAuthorizeData>()
//                         .ToArray();
//
//                     operation.OperationBehaviors.Add(
//                         new AuthorizationOperationBehavior(authorizeData, _authorizationPolicyProvider,
//                             _authorizationService, hasAllowAnonymous));
//
//                 }
//             }
//         }
//
//         private static MethodInfo GetServiceImplMethodInfo(ServiceDescription serviceDescription, OperationDescription operationDescription)
//         {
//             Type serviceType = serviceDescription.ServiceType;
//             MethodInfo[] methodInfos = serviceType.GetMethods();
//             return methodInfos.First(x => x.Name == operationDescription.Name);
//         }
//     }
// }
