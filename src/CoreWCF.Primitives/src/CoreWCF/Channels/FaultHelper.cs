// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Runtime;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System;
using System.Threading.Tasks;

namespace CoreWCF.Channels
{
    internal abstract class FaultHelper
    {
        protected FaultHelper()
        {
        }

        protected object ThisLock { get; } = new object();

        public abstract void Abort();

        public static bool AddressReply(Message message, Message faultMessage)
        {
            try
            {
                RequestReplyCorrelator.PrepareReply(faultMessage, message);
            }
            catch (MessageHeaderException exception)
            {
                // swallow it - we don't need to correlate the reply if the MessageId header is bad
                //if (DiagnosticUtility.ShouldTraceInformation)
                //    DiagnosticUtility.TraceHandledException(exception, TraceEventType.Information);
            }

            bool sendFault = true;
            try
            {
                sendFault = RequestReplyCorrelator.AddressReply(faultMessage, message);
            }
            catch (MessageHeaderException exception)
            {
                // swallow it - we don't need to address the reply if the addressing headers are bad
                //if (DiagnosticUtility.ShouldTraceInformation)
                //    DiagnosticUtility.TraceHandledException(exception, TraceEventType.Information);
            }

            return sendFault;
        }

        public abstract Task CloseAsync(CancellationToken cancellationToken);
        public abstract Task SendFaultAsync(IReliableChannelBinder binder, RequestContext requestContext, Message faultMessage);
    }

    internal abstract class TypedFaultHelper<TState> : FaultHelper
    {
        private InterruptibleWaitObject _closeHandle;
        private TimeSpan _defaultCloseTimeout;
        private TimeSpan _defaultSendTimeout;
        private Dictionary<IReliableChannelBinder, TState> _faultList = new Dictionary<IReliableChannelBinder, TState>();
        private AsyncCallback _onBinderCloseComplete;
        private AsyncCallback _onSendFaultComplete;
        private Action<object> _sendFaultCallback;

        protected TypedFaultHelper(TimeSpan defaultSendTimeout, TimeSpan defaultCloseTimeout)
        {
            _defaultSendTimeout = defaultSendTimeout;
            _defaultCloseTimeout = defaultCloseTimeout;
        }

        public override void Abort()
        {
            Dictionary<IReliableChannelBinder, TState> tempFaultList;
            InterruptibleWaitObject tempCloseHandle;

            lock (ThisLock)
            {
                tempFaultList = _faultList;
                _faultList = null;
                tempCloseHandle = _closeHandle;
            }

            if ((tempFaultList == null) || (tempFaultList.Count == 0))
            {
                if (tempCloseHandle != null)
                    tempCloseHandle.Set();
                return;
            }

            foreach (KeyValuePair<IReliableChannelBinder, TState> pair in tempFaultList)
            {
                AbortState(pair.Value, true);
                pair.Key.Abort();
            }

            if (tempCloseHandle != null)
                tempCloseHandle.Set();
        }

        private void AbortBinder(IReliableChannelBinder binder)
        {
            try
            {
                binder.Abort();
            }
            finally
            {
                RemoveBinder(binder);
            }
        }

        private async Task CloseBinderAsync(IReliableChannelBinder binder)
        {
            try
            {
                await binder.CloseAsync(_defaultCloseTimeout);
            }
            finally
            {
                RemoveBinder(binder);
            }
        }

        protected abstract void AbortState(TState state, bool isOnAbortThread);

        private void AfterClose()
        {
            Abort();
        }

        private bool BeforeClose()
        {
            lock (ThisLock)
            {
                if ((_faultList == null) || (_faultList.Count == 0))
                    return true;

                _closeHandle = new InterruptibleWaitObject(false, false);
            }

            return false;
        }

        public override async Task CloseAsync(CancellationToken cancellationToken)
        {
            if (BeforeClose())
                return;

            await _closeHandle.WaitAsync(cancellationToken);
            AfterClose();
        }

        protected abstract TState GetState(RequestContext requestContext, Message faultMessage);

        protected abstract Task SendFaultCoreAsync(IReliableChannelBinder binder, TState state, TimeSpan timeout);

        protected void RemoveBinder(IReliableChannelBinder binder)
        {
            InterruptibleWaitObject tempCloseHandle;

            lock (ThisLock)
            {
                if (_faultList == null)
                    return;

                _faultList.Remove(binder);
                if ((_closeHandle == null) || (_faultList.Count > 0))
                    return;

                // Close has been called.
                _faultList = null;
                tempCloseHandle = _closeHandle;
            }

            tempCloseHandle.Set();
        }

        protected async Task SendFaultAsync(IReliableChannelBinder binder, TState state)
        {
            bool throwing = true;

            try
            {
                await SendFaultCoreAsync(binder, state, _defaultSendTimeout);
                throwing = false;
            }
            finally
            {
                if (throwing)
                {
                    AbortState(state, false);
                    AbortBinder(binder);
                }
            }

            await CloseBinderAsync(binder);
        }

        public override async Task SendFaultAsync(IReliableChannelBinder binder, RequestContext requestContext, Message faultMessage)
        {
            try
            {
                bool abort = true;
                TState state = GetState(requestContext, faultMessage);

                lock (ThisLock)
                {
                    if (_faultList != null)
                    {
                        abort = false;
                        _faultList.Add(binder, state);
                    }
                }

                if (abort)
                {
                    AbortState(state, false);
                    binder.Abort();
                }

                await TaskHelpers.EnsureDefaultTaskScheduler();
                await SendFaultAsync(binder, state);
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }

                binder.HandleException(e);
            }
        }
    }

    internal struct FaultState
    {
        private Message faultMessage;
        private RequestContext requestContext;

        public FaultState(RequestContext requestContext, Message faultMessage)
        {
            this.requestContext = requestContext;
            this.faultMessage = faultMessage;
        }

        public Message FaultMessage { get { return faultMessage; } }
        public RequestContext RequestContext { get { return requestContext; } }
    }

    internal class ReplyFaultHelper : TypedFaultHelper<FaultState>
    {
        public ReplyFaultHelper(TimeSpan defaultSendTimeout, TimeSpan defaultCloseTimeout)
            : base(defaultSendTimeout, defaultCloseTimeout)
        {
        }

        protected override void AbortState(FaultState faultState, bool isOnAbortThread)
        {
            // if abort is true, the request could be in the middle of the encoding step, let the sending thread clean up.
            if (!isOnAbortThread)
            {
                faultState.FaultMessage.Close();
            }
            faultState.RequestContext.Abort();
        }

        protected override async Task SendFaultCoreAsync(IReliableChannelBinder binder, FaultState faultState, TimeSpan timeout)
        {
            await faultState.RequestContext.ReplyAsync(faultState.FaultMessage, TimeoutHelper.GetCancellationToken(timeout));
            faultState.FaultMessage.Close();
        }

        protected override FaultState GetState(RequestContext requestContext, Message faultMessage)
        {
            return new FaultState(requestContext, faultMessage);
        }
    }

    internal class SendFaultHelper : TypedFaultHelper<Message>
    {
        public SendFaultHelper(TimeSpan defaultSendTimeout, TimeSpan defaultCloseTimeout)
            : base(defaultSendTimeout, defaultCloseTimeout)
        {
        }

        protected override void AbortState(Message message, bool isOnAbortThread)
        {
            // if abort is true, the request could be in the middle of the encoding step, let the sending thread clean up.
            if (!isOnAbortThread)
            {
                message.Close();
            }
        }

        protected override async Task SendFaultCoreAsync(IReliableChannelBinder binder, Message message, TimeSpan timeout)
        {
            await binder.SendAsync(message, timeout);
            message.Close();
        }

        protected override Message GetState(RequestContext requestContext, Message faultMessage)
        {
            return faultMessage;
        }
    }
}
