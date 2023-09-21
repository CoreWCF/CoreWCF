// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Channels;
using CoreWCF.Description;
using Microsoft.Extensions.DependencyInjection;

namespace CoreWCF.Dispatcher
{
    public class OperationInvokerBehavior : IOperationBehavior
    {
        public OperationInvokerBehavior()
        {
        }

        void IOperationBehavior.Validate(OperationDescription description)
        {
        }

        void IOperationBehavior.AddBindingParameters(OperationDescription description, BindingParameterCollection parameters)
        {
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
                dispatch.Invoker = new TaskMethodInvoker(dispatch.ServiceProvider, description.TaskMethod, description.TaskTResult);
            }
            else if (description.SyncMethod != null)
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
                    dispatch.Invoker = new SyncMethodInvoker(dispatch.ServiceProvider, description.SyncMethod);
                    //}
                }
                else
                {
                    // only sync method is present on the contract
                    dispatch.Invoker = new SyncMethodInvoker(dispatch.ServiceProvider, description.SyncMethod);
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
