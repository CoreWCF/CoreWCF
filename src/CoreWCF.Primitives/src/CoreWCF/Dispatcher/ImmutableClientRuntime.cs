// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Reflection;
using CoreWCF.Runtime;
using CoreWCF.Telemetry;

namespace CoreWCF.Dispatcher
{
    internal class ImmutableClientRuntime
    {
        private readonly IChannelInitializer[] _channelInitializers;
        private readonly IClientMessageInspector[] _messageInspectors;
        private readonly Dictionary<string, ProxyOperationRuntime> _operations;
        private readonly Dictionary<string, ActionMetadata> _actionMappings;
        internal ImmutableClientRuntime(ClientRuntime behavior)
        {
            _channelInitializers = EmptyArray<IChannelInitializer>.ToArray(behavior.ChannelInitializers);
            //this.interactiveChannelInitializers = EmptyArray<IInteractiveChannelInitializer>.ToArray(behavior.InteractiveChannelInitializers);
            _messageInspectors = EmptyArray<IClientMessageInspector>.ToArray(behavior.MessageInspectors);

            OperationSelector = behavior.OperationSelector;
            UseSynchronizationContext = behavior.UseSynchronizationContext;
            ValidateMustUnderstand = behavior.ValidateMustUnderstand;

            UnhandledProxyOperation = new ProxyOperationRuntime(behavior.UnhandledClientOperation, this);

            //this.addTransactionFlowProperties = behavior.AddTransactionFlowProperties;

            _operations = new Dictionary<string, ProxyOperationRuntime>();
            _actionMappings = new Dictionary<string, ActionMetadata>();
            for (int i = 0; i < behavior.Operations.Count; i++)
            {
                ClientOperation operation = behavior.Operations[i];
                ProxyOperationRuntime operationRuntime = new ProxyOperationRuntime(operation, this);
                _operations.Add(operation.Name, operationRuntime);
            }

            foreach (var clientOperation in behavior.ClientOperations)
            {
                _actionMappings[clientOperation.Action] = new ActionMetadata(
                    contractName: $"{behavior.ContractNamespace}{behavior.ContractName}",
                    operationName: clientOperation.Name);
            }


            CorrelationCount = _messageInspectors.Length + behavior.MaxParameterInspectors;
        }

        internal int MessageInspectorCorrelationOffset
        {
            get { return 0; }
        }

        internal int ParameterInspectorCorrelationOffset
        {
            get { return _messageInspectors.Length; }
        }

        internal int CorrelationCount { get; }

        internal IClientOperationSelector OperationSelector { get; }

        internal ProxyOperationRuntime UnhandledProxyOperation { get; }

        internal bool UseSynchronizationContext { get; }

        internal bool ValidateMustUnderstand { get; set; }

        internal void AfterReceiveReply(ref ProxyRpc rpc)
        {
            int offset = MessageInspectorCorrelationOffset;
            try
            {
                for (int i = 0; i < _messageInspectors.Length; i++)
                {
                    _messageInspectors[i].AfterReceiveReply(ref rpc.Reply, rpc.Correlation[offset + i]);
                    //if (TD.ClientMessageInspectorAfterReceiveInvokedIsEnabled())
                    //{
                    //    TD.ClientMessageInspectorAfterReceiveInvoked(rpc.EventTraceActivity, this.messageInspectors[i].GetType().FullName);
                    //}
                }
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }
                if (ErrorBehavior.ShouldRethrowClientSideExceptionAsIs(e))
                {
                    throw;
                }
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperCallback(e);
            }
        }

        internal void BeforeSendRequest(ref ProxyRpc rpc)
        {
            int offset = MessageInspectorCorrelationOffset;
            try
            {
                rpc.Request.Properties.Add(TelemetryContextMessageProperty.Name, new TelemetryContextMessageProperty(_actionMappings));

                for (int i = 0; i < _messageInspectors.Length; i++)
                {
                    rpc.Correlation[offset + i] = _messageInspectors[i].BeforeSendRequest(ref rpc.Request, (IClientChannel)rpc.Channel.Proxy);
                    //if (TD.ClientMessageInspectorBeforeSendInvokedIsEnabled())
                    //{
                    //    TD.ClientMessageInspectorBeforeSendInvoked(rpc.EventTraceActivity, this.messageInspectors[i].GetType().FullName);
                    //}
                }
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }
                if (ErrorBehavior.ShouldRethrowClientSideExceptionAsIs(e))
                {
                    throw;
                }
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperCallback(e);
            }

            //if (this.addTransactionFlowProperties)
            //{
            //    SendTransaction(ref rpc);
            //}
        }

        // this should not be inlined, since we want to JIT the reference to System.Transactions
        // only if transactions are being flowed.
        //[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        //static void SendTransaction(ref ProxyRpc rpc)
        //{
        //    System.ServiceModel.Channels.TransactionFlowProperty.Set(Transaction.Current, rpc.Request);
        //}

        internal void InitializeChannel(IClientChannel channel)
        {
            try
            {
                for (int i = 0; i < _channelInitializers.Length; ++i)
                {
                    _channelInitializers[i].Initialize(channel);
                }
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }
                if (ErrorBehavior.ShouldRethrowClientSideExceptionAsIs(e))
                {
                    throw;
                }
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperCallback(e);
            }
        }

        internal ProxyOperationRuntime GetOperation(MethodBase methodBase, object[] args, out bool canCacheResult)
        {
            if (OperationSelector == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException
                                                        (SR.Format(SR.SFxNeedProxyBehaviorOperationSelector2, methodBase.Name,
                                                                      methodBase.DeclaringType.Name)));
            }

            try
            {
                if (OperationSelector.AreParametersRequiredForSelection)
                {
                    canCacheResult = false;
                }
                else
                {
                    args = null;
                    canCacheResult = true;
                }
                string operationName = OperationSelector.SelectOperation(methodBase, args);
                if ((operationName != null) && _operations.TryGetValue(operationName, out ProxyOperationRuntime operation))
                {
                    return operation;
                }
                else
                {
                    // did not find the right operation, will not know how
                    // to invoke the method.
                    return null;
                }
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }
                if (ErrorBehavior.ShouldRethrowClientSideExceptionAsIs(e))
                {
                    throw;
                }
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperCallback(e);
            }
        }

        internal ProxyOperationRuntime GetOperationByName(string operationName)
        {
            if (_operations.TryGetValue(operationName, out ProxyOperationRuntime operation))
            {
                return operation;
            }
            else
            {
                return null;
            }
        }
    }
}
