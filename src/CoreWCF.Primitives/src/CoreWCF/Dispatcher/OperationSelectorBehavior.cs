// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Reflection;
using CoreWCF.Channels;
using CoreWCF.Description;

namespace CoreWCF.Dispatcher
{
    internal class OperationSelectorBehavior : IContractBehavior
    {
        void IContractBehavior.Validate(ContractDescription description, ServiceEndpoint endpoint)
        {
        }

        void IContractBehavior.AddBindingParameters(ContractDescription description, ServiceEndpoint endpoint, BindingParameterCollection parameters)
        {
        }

        void IContractBehavior.ApplyDispatchBehavior(ContractDescription description, ServiceEndpoint endpoint, DispatchRuntime dispatch)
        {
            if (dispatch.ClientRuntime != null)
            {
                dispatch.ClientRuntime.OperationSelector = new MethodInfoOperationSelector(description, MessageDirection.Output);
            }
        }

        void IContractBehavior.ApplyClientBehavior(ContractDescription description, ServiceEndpoint endpoint, ClientRuntime proxy)
        {
            proxy.OperationSelector = new MethodInfoOperationSelector(description, MessageDirection.Input);
        }

        internal class MethodInfoOperationSelector : IClientOperationSelector
        {
            private readonly Dictionary<object, string> operationMap;

            internal MethodInfoOperationSelector(ContractDescription description, MessageDirection directionThatRequiresClientOpSelection)
            {
                operationMap = new Dictionary<object, string>();

                for (int i = 0; i < description.Operations.Count; i++)
                {
                    OperationDescription operation = description.Operations[i];
                    if (operation.Messages[0].Direction == directionThatRequiresClientOpSelection)
                    {
                        if (operation.SyncMethod != null)
                        {
                            if (!operationMap.ContainsKey(operation.SyncMethod))
                            {
                                operationMap.Add(operation.SyncMethod, operation.Name);
                            }
                        }

                        if (operation.BeginMethod != null)
                        {
                            if (!operationMap.ContainsKey(operation.BeginMethod))
                            {
                                operationMap.Add(operation.BeginMethod, operation.Name);
                                operationMap.Add(operation.EndMethod, operation.Name);
                            }
                        }

                        if (operation.TaskMethod != null)
                        {
                            if (!operationMap.ContainsKey(operation.TaskMethod))
                            {
                                operationMap.Add(operation.TaskMethod, operation.Name);
                            }
                        }
                    }
                }
            }

            public bool AreParametersRequiredForSelection
            {
                get { return false; }
            }

            public string SelectOperation(MethodBase method, object[] parameters)
            {
                if (operationMap.ContainsKey(method))
                {
                    return operationMap[method];
                }
                else
                {
                    return null;
                }
            }
        }
    }

}