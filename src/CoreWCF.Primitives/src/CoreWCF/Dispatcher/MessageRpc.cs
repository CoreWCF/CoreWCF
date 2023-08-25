// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.Channels;
using CoreWCF.Runtime;

namespace CoreWCF.Dispatcher
{
    internal delegate Task<MessageRpc> MessageRpcProcessor(MessageRpc rpc);

    internal delegate Task MessageRpcErrorHandler(MessageRpc rpc);

    // TODO: Pool MessageRpc objects. These are zero cost on .NET Framework as it's a struct but passing things by ref is problematic
    // when using async/await. This causes an allocation per request so pool them to remove that allocation.
    internal class MessageRpc
    {
        internal readonly ServiceChannel Channel;
        internal readonly ChannelHandler ChannelHandler;
        internal readonly object[] Correlation;
        internal readonly ServiceHostBase Host;
        internal readonly OperationContext OperationContext;
        //internal ServiceModelActivity Activity;
        internal Guid ResponseActivityId;
        internal IAsyncResult AsyncResult;
        internal Task TaskResult;
        internal bool CanSendReply;
        internal bool SuccessfullySendReply;
        internal object[] InputParameters;
        internal object[] OutputParameters;
        internal object ReturnParameter;
        internal bool ParametersDisposed;
        internal bool DidDeserializeRequestBody;
        //internal TransactionMessageProperty TransactionMessageProperty;
        //internal TransactedBatchContext TransactedBatchContext;
        internal Exception Error;
        internal MessageRpcErrorHandler ErrorProcessor;
        internal ErrorHandlerFaultInfo FaultInfo;
        internal bool HasSecurityContext;
        internal object Instance;
        internal bool MessageRpcOwnsInstanceContextThrottle;
        internal MessageRpcProcessor AsyncProcessor;
        internal Collection<MessageHeaderInfo> NotUnderstoodHeaders;
        internal DispatchOperationRuntime Operation;
        internal Message Request;
        internal RequestContext RequestContext;
        internal bool RequestContextThrewOnReply;
        internal UniqueId RequestID;
        internal Message Reply;
        internal TimeoutHelper ReplyTimeoutHelper;
        internal RequestReplyCorrelator.ReplyToInfo ReplyToInfo;
        internal MessageVersion RequestVersion;
        internal ServiceSecurityContext SecurityContext;
        internal InstanceContext InstanceContext;
        internal bool SuccessfullyBoundInstance;
        internal bool SuccessfullyIncrementedActivity;
        internal bool SuccessfullyLockedInstance;
        internal /* ReceiveContextRPCFacet */ ReceiveContext ReceiveContext;
        //internal TransactionRpcFacet transaction;
        //internal IAspNetMessageProperty HostingProperty;
        //internal MessageRpcInvokeNotification InvokeNotification;
        //internal EventTraceActivity EventTraceActivity;
        internal bool _processCallReturned;
        private bool _isInstanceContextSingleton;
        private SignalGate<IAsyncResult> _invokeContinueGate;

        internal MessageRpc(RequestContext requestContext, Message request, DispatchOperationRuntime operation,
            ServiceChannel channel, ServiceHostBase host, ChannelHandler channelHandler, bool cleanThread,
            OperationContext operationContext, InstanceContext instanceContext/*, EventTraceActivity eventTraceActivity*/)
        {
            Fx.Assert((operationContext != null), "correwcf.Dispatcher.MessageRpc.MessageRpc(), operationContext == null");
            // TODO: ChannelHandler supplied an ErrorHandler, need to supply this some other way.
            //Fx.Assert(channelHandler != null, "System.ServiceModel.Dispatcher.MessageRpc.MessageRpc(), channelHandler == null");

            //this.Activity = null;
            //this.EventTraceActivity = eventTraceActivity;
            AsyncResult = null;
            TaskResult = null;
            CanSendReply = true;
            Channel = channel;
            ChannelHandler = channelHandler;
            Correlation = EmptyArray.Allocate(operation.Parent.CorrelationCount);
            DidDeserializeRequestBody = false;
            Error = null;
            ErrorProcessor = null;
            FaultInfo = new ErrorHandlerFaultInfo(request.Version.Addressing.DefaultFaultAction);
            HasSecurityContext = false;
            Host = host;
            Instance = null;
            AsyncProcessor = null;
            NotUnderstoodHeaders = null;
            Operation = operation;
            OperationContext = operationContext;
            IsPaused = false;
            ParametersDisposed = false;
            ReceiveContext = null;
            Request = request;
            RequestContext = requestContext;
            RequestContextThrewOnReply = false;
            SuccessfullySendReply = false;
            RequestVersion = request.Version;
            Reply = null;
            ReplyTimeoutHelper = new TimeoutHelper();
            SecurityContext = null;
            InstanceContext = instanceContext;
            SuccessfullyBoundInstance = false;
            SuccessfullyIncrementedActivity = false;
            SuccessfullyLockedInstance = false;
            SwitchedThreads = !cleanThread;
            //this.transaction = null;
            InputParameters = null;
            OutputParameters = null;
            ReturnParameter = null;
            _isInstanceContextSingleton = InstanceContextProviderBase.IsProviderSingleton(Channel.DispatchRuntime.InstanceContextProvider);
            _invokeContinueGate = null;

            if (!operation.IsOneWay && !operation.Parent.ManualAddressing)
            {
                RequestID = request.Headers.MessageId;
                ReplyToInfo = new RequestReplyCorrelator.ReplyToInfo(request);
            }
            else
            {
                RequestID = null;
                ReplyToInfo = new RequestReplyCorrelator.ReplyToInfo();
            }

            //if (DiagnosticUtility.ShouldUseActivity)
            //{
            //    this.Activity = TraceUtility.ExtractActivity(this.Request);
            //}

            //if (DiagnosticUtility.ShouldUseActivity || TraceUtility.ShouldPropagateActivity)
            //{
            //    this.ResponseActivityId = ActivityIdHeader.ExtractActivityId(this.Request);
            //}
            //else
            //{
            ResponseActivityId = Guid.Empty;
            //}

            //if (this.EventTraceActivity == null && FxTrace.Trace.IsEnd2EndActivityTracingEnabled)
            //{
            //    if (this.Request != null)
            //    {
            //        this.EventTraceActivity = EventTraceActivityHelper.TryExtractActivity(this.Request, true);
            //    }
            //}
        }

        internal bool IsPaused { get; private set; }

        internal bool SwitchedThreads { get; }

        internal bool IsInstanceContextSingleton
        {
            set
            {
                _isInstanceContextSingleton = value;
            }
        }

        //internal TransactionRpcFacet Transaction
        //{
        //    get
        //    {
        //        if (this.transaction == null)
        //        {
        //            this.transaction = new TransactionRpcFacet(ref this);
        //        }
        //        return this.transaction;
        //    }
        //}

        internal async ValueTask AbortAsync()
        {
            await AbortRequestContextAsync();
            AbortChannel();
            AbortInstanceContext();
        }

        private async ValueTask AbortRequestContextAsync(RequestContext requestContext)
        {
            try
            {
                requestContext.Abort();

                /* ReceiveContextRPCFacet */ ReceiveContext receiveContext = ReceiveContext;

                if (receiveContext != null)
                {
                    ReceiveContext = null;

                    await receiveContext.AbandonAsync(CancellationToken.None);
                }
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }

                ChannelHandler.HandleError(e);
            }
        }

        internal async ValueTask AbortRequestContextAsync()
        {
            if (OperationContext.RequestContext != null)
            {
                await AbortRequestContextAsync(OperationContext.RequestContext);
            }
            if ((RequestContext != null) && (RequestContext != OperationContext.RequestContext))
            {
                await AbortRequestContextAsync(RequestContext);
            }

            TraceCallDurationInDispatcherIfNecessary(false);
        }

        private void TraceCallDurationInDispatcherIfNecessary(bool requestContextWasClosedSuccessfully)
        {
            // only need to trace once (either for the failure or success case)
            //if (TD.DispatchFailedIsEnabled())
            //{
            //    if (requestContextWasClosedSuccessfully)
            //    {
            //        TD.DispatchSuccessful(this.EventTraceActivity, this.Operation.Name);
            //    }
            //    else
            //    {
            //        TD.DispatchFailed(this.EventTraceActivity, this.Operation.Name);
            //    }
            //}
        }

        internal async Task CloseRequestContextAsync()
        {
            if (OperationContext.RequestContext != null)
            {
                await DisposeRequestContextAsync(OperationContext.RequestContext);
            }
            if ((RequestContext != null) && (RequestContext != OperationContext.RequestContext))
            {
                await DisposeRequestContextAsync(RequestContext);
            }
            TraceCallDurationInDispatcherIfNecessary(true);
        }

        private async ValueTask DisposeRequestContextAsync(RequestContext context)
        {
            try
            {
                await context.CloseAsync();

                /* ReceiveContextRPCFacet */ ReceiveContext receiveContext = ReceiveContext;
                if (receiveContext != null)
                {
                    ReceiveContext = null;
                    await receiveContext.CompleteAsync(CancellationToken.None);
                }
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }

                await AbortRequestContextAsync(context);
                ChannelHandler.HandleError(e);
            }
        }

        internal void AbortChannel()
        {
            if ((Channel != null) && Channel.HasSession)
            {
                try
                {
                    Channel.Abort();
                }
                catch (Exception e)
                {
                    if (Fx.IsFatal(e))
                    {
                        throw;
                    }

                    ChannelHandler.HandleError(e);
                }
            }
        }

        internal async Task CloseChannelAsync()
        {
            if ((Channel != null) && Channel.HasSession)
            {
                try
                {
                    var helper = new TimeoutHelper(ChannelHandler.CloseAfterFaultTimeout);
                    await Channel.CloseAsync(helper.GetCancellationToken());
                }
                catch (Exception e)
                {
                    if (Fx.IsFatal(e))
                    {
                        throw;
                    }

                    ChannelHandler.HandleError(e);
                }
            }
        }

        internal void AbortInstanceContext()
        {
            if (InstanceContext != null && !_isInstanceContextSingleton)
            {
                try
                {
                    InstanceContext.Abort();
                }
                catch (Exception e)
                {
                    if (Fx.IsFatal(e))
                    {
                        throw;
                    }

                    ChannelHandler.HandleError(e);
                }
            }
        }

        internal void EnsureReceive()
        {
            //using (ServiceModelActivity.BoundOperation(this.Activity))
            //{
            ChannelHandler.EnsureReceive();
            //}
        }

        private bool ProcessError(Exception e)
        {
            MessageRpcErrorHandler handler = ErrorProcessor;
            try
            {
                Type exceptionType = e.GetType();

                if (exceptionType.IsAssignableFrom(typeof(FaultException)))
                {
                    DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
                }
                else
                {
                    DiagnosticUtility.TraceHandledException(e, TraceEventType.Error);
                }

                //if (TraceUtility.MessageFlowTracingOnly)
                //{
                //    TraceUtility.SetActivityId(this.Request.Properties);
                //    if (Guid.Empty == DiagnosticTraceBase.ActivityId)
                //    {
                //        Guid receivedActivityId = TraceUtility.ExtractActivityId(this.Request);
                //        if (Guid.Empty != receivedActivityId)
                //        {
                //            DiagnosticTraceBase.ActivityId = receivedActivityId;
                //        }
                //    }
                //}


                Error = e;

                if (ErrorProcessor != null)
                {
                    ErrorProcessor(this);
                }

                return (Error == null);
            }
            catch (Exception e2)
            {
                if (Fx.IsFatal(e2))
                {
                    throw;
                }

                return ((handler != ErrorProcessor) && ProcessError(e2));
            }
        }

        internal void DisposeParameters(bool excludeInput)
        {
            if (Operation.DisposeParameters)
            {
                DisposeParametersCore(excludeInput);
            }
        }

        internal void DisposeParametersCore(bool excludeInput)
        {
            if (!ParametersDisposed)
            {
                if (!excludeInput)
                {
                    DisposeParameterList(InputParameters);
                }

                DisposeParameterList(OutputParameters);

                if (ReturnParameter is IDisposable disposableParameter)
                {
                    try
                    {
                        disposableParameter.Dispose();
                    }
                    catch (Exception e)
                    {
                        if (Fx.IsFatal(e))
                        {
                            throw;
                        }

                        ChannelHandler.HandleError(e);
                    }
                }

                ParametersDisposed = true;
            }
        }

        private void DisposeParameterList(object[] parameters)
        {
            if (parameters != null)
            {
                foreach (object obj in parameters)
                {
                    if (obj is IDisposable disposableParameter)
                    {
                        try
                        {
                            disposableParameter.Dispose();
                        }
                        catch (Exception e)
                        {
                            if (Fx.IsFatal(e))
                            {
                                throw;
                            }

                            ChannelHandler.HandleError(e);
                        }
                    }
                }
            }
        }

        internal async Task<MessageRpc> ProcessAsync(bool isOperationContextSet)
        {
            MessageRpc result = this;
            //using (ServiceModelActivity.BoundOperation(this.Activity))
            //{
            // bool completed = true;

            OperationContext originalContext;
            if (!isOperationContextSet)
            {
                originalContext = OperationContext.Current;
            }
            else
            {
                originalContext = null;
            }
            IncrementBusyCount();

            try
            {
                if (!isOperationContextSet)
                {
                    OperationContext.Current = OperationContext;
                }

                await AsyncProcessor(this);

                OperationContext.SetClientReply(null, false);
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }
                if (!ProcessError(e) && FaultInfo.Fault == null)
                {
                    await AbortAsync();
                }
            }
            finally
            {
                try
                {
                    DecrementBusyCount();

                    if (!isOperationContextSet)
                    {
                        OperationContext.Current = originalContext;
                    }

                    OperationContext.ClearClientReplyNoThrow();
                }
                catch (Exception e)
                {
                    if (Fx.IsFatal(e))
                    {
#pragma warning disable CA2219 // Do not raise exceptions in finally clauses - Fx.IsFatal filters out non-process ending exceptions
                        throw;
#pragma warning restore CA2219 // Do not raise exceptions in finally clauses
                    }
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperFatal(e.Message, e);
                }
            }

            return this;
            //}
        }

        // UnPause is called on the original MessageRpc to continue work on the current thread, and the copy is ignored.
        // Since the copy is ignored, Decrement the BusyCount
        internal void UnPause()
        {
            IsPaused = false;
            DecrementBusyCount();
        }

        internal bool UnlockInvokeContinueGate(out IAsyncResult result)
        {
            return _invokeContinueGate.Unlock(out result);
        }

        internal void PrepareInvokeContinueGate()
        {
            _invokeContinueGate = new SignalGate<IAsyncResult>();
        }

        private void IncrementBusyCount()
        {
            // TODO: Do we want a way to keep track of bust count? I believe this originally drove PerformanceCounters so we might want to re-work this functionality.
            // Only increment the counter on the service side.
            //if (Host != null)
            //{
            //Host.IncrementBusyCount();
            //if (AspNetEnvironment.Current.TraceIncrementBusyCountIsEnabled())
            //{
            //    AspNetEnvironment.Current.TraceIncrementBusyCount(SR.Format(SR.ServiceBusyCountTrace, this.Operation.Action));
            //}
            //}
        }

        private void DecrementBusyCount()
        {
            // See comment on IncrementBusyCount
            //if (Host != null)
            //{
            //    Host.DecrementBusyCount();
            //if (AspNetEnvironment.Current.TraceDecrementBusyCountIsEnabled())
            //{
            //    AspNetEnvironment.Current.TraceDecrementBusyCount(SR.Format(SR.ServiceBusyCountTrace, this.Operation.Action));
            //}
            //}
        }
    }
}
