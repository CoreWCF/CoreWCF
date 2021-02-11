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
            private readonly Dictionary<object, string> _operationMap;

            internal MethodInfoOperationSelector(ContractDescription description, MessageDirection directionThatRequiresClientOpSelection)
            {
                _operationMap = new Dictionary<object, string>();

                for (int i = 0; i < description.Operations.Count; i++)
                {
                    OperationDescription operation = description.Operations[i];
                    if (operation.Messages[0].Direction == directionThatRequiresClientOpSelection)
                    {
                        if (operation.SyncMethod != null)
                        {
                            if (!_operationMap.ContainsKey(operation.SyncMethod))
                            {
                                _operationMap.Add(operation.SyncMethod, operation.Name);
                            }
                        }

                        if (operation.BeginMethod != null)
                        {
                            if (!_operationMap.ContainsKey(operation.BeginMethod))
                            {
                                _operationMap.Add(operation.BeginMethod, operation.Name);
                                _operationMap.Add(operation.EndMethod, operation.Name);
                            }
                        }

                        if (operation.TaskMethod != null)
                        {
                            if (!_operationMap.ContainsKey(operation.TaskMethod))
                            {
                                _operationMap.Add(operation.TaskMethod, operation.Name);
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
                if (_operationMap.ContainsKey(method))
                {
                    return _operationMap[method];
                }
                else
                {
                    return null;
                }
            }
        }
    }
}