// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Reflection;
using CoreWCF.Runtime;

namespace CoreWCF.Dispatcher
{
    internal class ImmutableClientRuntime
    {
        private readonly IChannelInitializer[] channelInitializers;
        private readonly IClientMessageInspector[] messageInspectors;
        private readonly Dictionary<string, ProxyOperationRuntime> operations;
        private readonly bool useSynchronizationContext;

        internal ImmutableClientRuntime(ClientRuntime behavior)
        {
            channelInitializers = EmptyArray<IChannelInitializer>.ToArray(behavior.ChannelInitializers);
            //this.interactiveChannelInitializers = EmptyArray<IInteractiveChannelInitializer>.ToArray(behavior.InteractiveChannelInitializers);
            messageInspectors = EmptyArray<IClientMessageInspector>.ToArray(behavior.MessageInspectors);

            OperationSelector = behavior.OperationSelector;
            useSynchronizationContext = behavior.UseSynchronizationContext;
            ValidateMustUnderstand = behavior.ValidateMustUnderstand;

            UnhandledProxyOperation = new ProxyOperationRuntime(behavior.UnhandledClientOperation, this);

            //this.addTransactionFlowProperties = behavior.AddTransactionFlowProperties;

            operations = new Dictionary<string, ProxyOperationRuntime>();

            for (int i = 0; i < behavior.Operations.Count; i++)
            {
                ClientOperation operation = behavior.Operations[i];
                ProxyOperationRuntime operationRuntime = new ProxyOperationRuntime(operation, this);
                operations.Add(operation.Name, operationRuntime);
            }

            CorrelationCount = messageInspectors.Length + behavior.MaxParameterInspectors;
        }

        internal int MessageInspectorCorrelationOffset
        {
            get { return 0; }
        }

        internal int ParameterInspectorCorrelationOffset
        {
            get { return messageInspectors.Length; }
        }

        internal int CorrelationCount { get; }

        internal IClientOperationSelector OperationSelector { get; }

        internal ProxyOperationRuntime UnhandledProxyOperation { get; }

        internal bool UseSynchronizationContext
        {
            get { return useSynchronizationContext; }
        }

        internal bool ValidateMustUnderstand { get; set; }

        internal void AfterReceiveReply(ref ProxyRpc rpc)
        {
            int offset = MessageInspectorCorrelationOffset;
            try
            {
                for (int i = 0; i < messageInspectors.Length; i++)
                {
                    messageInspectors[i].AfterReceiveReply(ref rpc.Reply, rpc.Correlation[offset + i]);
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
                for (int i = 0; i < messageInspectors.Length; i++)
                {
                    rpc.Correlation[offset + i] = messageInspectors[i].BeforeSendRequest(ref rpc.Request, (IClientChannel)rpc.Channel.Proxy);
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
                for (int i = 0; i < channelInitializers.Length; ++i)
                {
                    channelInitializers[i].Initialize(channel);
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
                if ((operationName != null) && operations.TryGetValue(operationName, out ProxyOperationRuntime operation))
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
            if (operations.TryGetValue(operationName, out ProxyOperationRuntime operation))
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