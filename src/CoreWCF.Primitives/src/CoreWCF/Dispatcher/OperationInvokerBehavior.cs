// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CoreWCF.Channels;
using CoreWCF.Description;
using Microsoft.Extensions.DependencyInjection;

namespace CoreWCF.Dispatcher
{
    public class OperationInvokerBehavior : IOperationBehavior
    {
        private IServiceProvider _serviceProvider;

        private static readonly Lazy<bool> s_isOperationInvokerGeneratorEnabled = new Lazy<bool>(() =>
        {
            if (AppContext.TryGetSwitch("CoreWCF.Dispatcher.UseGeneratedOperationInvokers", out var value))
            {
                return value;
            }

            Assembly entryAssembly = Assembly.GetEntryAssembly();
            // return null when running UT with Xunit and .NET Framework
            if (entryAssembly != null)
            {
                var assemblyAttributeArgsData = entryAssembly
                    .GetCustomAttributesData()
                    .Where(static data => data.AttributeType == typeof(EnableCoreWCFOperationInvokerGeneratorAttribute))
                    .SelectMany(static data => data.ConstructorArguments)
                    .Where(static data => data.ArgumentType == typeof(bool))
                    .ToList();

                if (assemblyAttributeArgsData.Count > 0)
                {
                    return assemblyAttributeArgsData[0].Value is true;
                }
            }

            return true;
        });

        public OperationInvokerBehavior()
        {

        }

        void IOperationBehavior.Validate(OperationDescription description)
        {
        }

        void IOperationBehavior.AddBindingParameters(OperationDescription description, BindingParameterCollection parameters)
        {
            _serviceProvider = parameters.Find<IServiceProvider>();
        }

        void IOperationBehavior.ApplyDispatchBehavior(OperationDescription description, DispatchOperation dispatch)
        {
            if (dispatch == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(dispatch));
            }
            if (description == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(description));
            }

            if (description.TaskMethod != null)
            {
                if (s_isOperationInvokerGeneratorEnabled.Value)
                {
                    if (DispatchOperationRuntimeHelpers.OperationInvokers.TryGetValue(DispatchOperationRuntimeHelpers.GetKey(description.TaskMethod), out IOperationInvoker invoker))
                    {
                        dispatch.Invoker = invoker;
                    }
                    else
                    {
                        // Fallback to reflection-based invoker if generated invoker not found
                        dispatch.Invoker = new TaskMethodInvoker(_serviceProvider, description.TaskMethod, description.TaskTResult);
                    }
                }
                else
                {
                    dispatch.Invoker = new TaskMethodInvoker(_serviceProvider, description.TaskMethod, description.TaskTResult);
                }

            }
            else if (description.SyncMethod != null)
            {
                if (s_isOperationInvokerGeneratorEnabled.Value)
                {
                    if (DispatchOperationRuntimeHelpers.OperationInvokers.TryGetValue(DispatchOperationRuntimeHelpers.GetKey(description.SyncMethod), out IOperationInvoker invoker))
                    {
                        dispatch.Invoker = invoker;
                    }
                    else
                    {
                        // Fallback to reflection-based invoker if generated invoker not found
                        if (description.BeginMethod != null)
                        {
                            // both sync and async methods are present on the contract
                            dispatch.Invoker = new SyncMethodInvoker(_serviceProvider, description.SyncMethod);
                        }
                        else
                        {
                            // only sync method is present on the contract
                            dispatch.Invoker = new SyncMethodInvoker(_serviceProvider, description.SyncMethod);
                        }
                    }
                }
                else
                {
                    if (description.BeginMethod != null)
                    {
                        // both sync and async methods are present on the contract, check the preference
                        //OperationBehaviorAttribute operationBehaviorAttribute = description.Behaviors.Find<OperationBehaviorAttribute>();
                        //if ((operationBehaviorAttribute != null) && operationBehaviorAttribute.PreferAsyncInvocation)
                        //{
                        //    dispatch.Invoker = new AsyncMethodInvoker(description.BeginMethod, description.EndMethod);
                        //}
                        //else
                        //{
                        dispatch.Invoker = new SyncMethodInvoker(_serviceProvider, description.SyncMethod);
                        //}
                    }
                    else
                    {
                        // only sync method is present on the contract
                        dispatch.Invoker = new SyncMethodInvoker(_serviceProvider, description.SyncMethod);
                    }
                }
            }
            else
            {
                if (description.BeginMethod != null)
                {
                    // only async method is present on the contract
                    throw new PlatformNotSupportedException();
                    //dispatch.Invoker = new AsyncMethodInvoker(description.BeginMethod, description.EndMethod);
                }
            }
        }

        void IOperationBehavior.ApplyClientBehavior(OperationDescription description, ClientOperation proxy)
        {
        }
    }
}
