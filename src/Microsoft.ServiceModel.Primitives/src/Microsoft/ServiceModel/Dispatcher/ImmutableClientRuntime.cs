using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Runtime;
using Microsoft.ServiceModel.Channels;

namespace Microsoft.ServiceModel.Dispatcher
{
    internal class ImmutableClientRuntime
    {
        int correlationCount;
        //bool addTransactionFlowProperties;
        //IInteractiveChannelInitializer[] interactiveChannelInitializers;
        IClientOperationSelector operationSelector;
        IChannelInitializer[] channelInitializers;
        IClientMessageInspector[] messageInspectors;
        Dictionary<string, ProxyOperationRuntime> operations;
        ProxyOperationRuntime unhandled;
        bool useSynchronizationContext;
        bool validateMustUnderstand;

        internal ImmutableClientRuntime(ClientRuntime behavior)
        {
            channelInitializers = EmptyArray<IChannelInitializer>.ToArray(behavior.ChannelInitializers);
            //this.interactiveChannelInitializers = EmptyArray<IInteractiveChannelInitializer>.ToArray(behavior.InteractiveChannelInitializers);
            messageInspectors = EmptyArray<IClientMessageInspector>.ToArray(behavior.MessageInspectors);

            operationSelector = behavior.OperationSelector;
            useSynchronizationContext = behavior.UseSynchronizationContext;
            validateMustUnderstand = behavior.ValidateMustUnderstand;

            unhandled = new ProxyOperationRuntime(behavior.UnhandledClientOperation, this);

            //this.addTransactionFlowProperties = behavior.AddTransactionFlowProperties;

            operations = new Dictionary<string, ProxyOperationRuntime>();

            for (int i = 0; i < behavior.Operations.Count; i++)
            {
                ClientOperation operation = behavior.Operations[i];
                ProxyOperationRuntime operationRuntime = new ProxyOperationRuntime(operation, this);
                operations.Add(operation.Name, operationRuntime);
            }

            correlationCount = messageInspectors.Length + behavior.MaxParameterInspectors;
        }

        internal int MessageInspectorCorrelationOffset
        {
            get { return 0; }
        }

        internal int ParameterInspectorCorrelationOffset
        {
            get { return messageInspectors.Length; }
        }

        internal int CorrelationCount
        {
            get { return correlationCount; }
        }

        internal IClientOperationSelector OperationSelector
        {
            get { return operationSelector; }
        }

        internal ProxyOperationRuntime UnhandledProxyOperation
        {
            get { return unhandled; }
        }

        internal bool UseSynchronizationContext
        {
            get { return useSynchronizationContext; }
        }

        internal bool ValidateMustUnderstand
        {
            get { return validateMustUnderstand; }
            set { validateMustUnderstand = value; }
        }

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
            if (operationSelector == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException
                                                        (SR.Format(SR.SFxNeedProxyBehaviorOperationSelector2, methodBase.Name,
                                                                      methodBase.DeclaringType.Name)));
            }

            try
            {
                if (operationSelector.AreParametersRequiredForSelection)
                {
                    canCacheResult = false;
                }
                else
                {
                    args = null;
                    canCacheResult = true;
                }
                string operationName = operationSelector.SelectOperation(methodBase, args);
                ProxyOperationRuntime operation;
                if ((operationName != null) && operations.TryGetValue(operationName, out operation))
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
            ProxyOperationRuntime operation = null;
            if (operations.TryGetValue(operationName, out operation))
                return operation;
            else
                return null;
        }
    }
}