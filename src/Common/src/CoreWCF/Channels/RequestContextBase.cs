// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Diagnostics;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    internal abstract class RequestContextBase : RequestContext
    {
        private TimeSpan _defaultSendTimeout;
        private TimeSpan _defaultCloseTimeout;
        private CommunicationState _state = CommunicationState.Opened;
        private Message _requestMessage;
        private Exception _requestMessageException;
        private bool _replySent;

        protected RequestContextBase(Message requestMessage, TimeSpan defaultCloseTimeout, TimeSpan defaultSendTimeout)
        {
            _defaultSendTimeout = defaultSendTimeout;
            _defaultCloseTimeout = defaultCloseTimeout;
            _requestMessage = requestMessage;
        }

        public void ReInitialize(Message requestMessage)
        {
            _state = CommunicationState.Opened;
            _requestMessageException = null;
            _replySent = false;
            ReplyInitiated = false;
            Aborted = false;
            _requestMessage = requestMessage;
        }

        public override Message RequestMessage
        {
            get
            {
                if (_requestMessageException != null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(_requestMessageException);
                }

                return _requestMessage;
            }
        }

        protected void SetRequestMessage(Message requestMessage)
        {
            Fx.Assert(_requestMessageException == null, "Cannot have both a requestMessage and a requestException.");
            _requestMessage = requestMessage;
        }

        protected void SetRequestMessage(Exception requestMessageException)
        {
            Fx.Assert(_requestMessage == null, "Cannot have both a requestMessage and a requestException.");
            _requestMessageException = requestMessageException;
        }

        protected bool ReplyInitiated { get; private set; }

        protected object ThisLock { get; } = new object();

        public bool Aborted { get; private set; }

        public TimeSpan DefaultCloseTimeout
        {
            get { return _defaultCloseTimeout; }
        }

        public TimeSpan DefaultSendTimeout
        {
            get { return _defaultSendTimeout; }
        }

        public override void Abort()
        {
            lock (ThisLock)
            {
                if (_state == CommunicationState.Closed)
                {
                    return;
                }

                _state = CommunicationState.Closing;

                Aborted = true;
            }

            //if (DiagnosticUtility.ShouldTraceWarning)
            //{
            //    TraceUtility.TraceEvent(TraceEventType.Warning, TraceCode.RequestContextAbort,
            //        SRCommon.Format(SRCommon.TraceCodeRequestContextAbort), this);
            //}

            try
            {
                OnAbort();
            }
            finally
            {
                _state = CommunicationState.Closed;
            }
        }

        public override Task CloseAsync()
        {
            var helper = new TimeoutHelper(_defaultCloseTimeout);
            return CloseAsync(helper.GetCancellationToken());
        }

        public override async Task CloseAsync(CancellationToken token)
        {
            bool sendAck = false;
            lock (ThisLock)
            {
                if (_state != CommunicationState.Opened)
                {
                    return;
                }

                if (TryInitiateReply())
                {
                    sendAck = true;
                }

                _state = CommunicationState.Closing;
            }

            bool throwing = true;

            try
            {
                if (sendAck)
                {
                    await OnReplyAsync(null, token);
                }

                await OnCloseAsync(token);
                _state = CommunicationState.Closed;
                throwing = false;
            }
            finally
            {
                if (throwing)
                {
                    Abort();
                }
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (!disposing)
            {
                return;
            }

            if (_replySent)
            {
                CloseAsync().GetAwaiter().GetResult();
            }
            else
            {
                Abort();
            }
        }

        protected abstract void OnAbort();
        protected abstract Task OnCloseAsync(CancellationToken token);
        protected abstract Task OnReplyAsync(Message message, CancellationToken token);

        protected void ThrowIfInvalidReply()
        {
            if (_state == CommunicationState.Closed || _state == CommunicationState.Closing)
            {
                if (Aborted)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CommunicationObjectAbortedException(SRCommon.RequestContextAborted));
                }
                else
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ObjectDisposedException(GetType().FullName));
                }
            }

            if (ReplyInitiated)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SRCommon.ReplyAlreadySent));
            }
        }

        /// <summary>
        /// Attempts to initiate the reply. If a reply is not initiated already (and the object is opened), 
        /// then it initiates the reply and returns true. Otherwise, it returns false.
        /// </summary>
        protected bool TryInitiateReply()
        {
            lock (ThisLock)
            {
                if ((_state != CommunicationState.Opened) || ReplyInitiated)
                {
                    return false;
                }
                else
                {
                    ReplyInitiated = true;
                    return true;
                }
            }
        }

        public override Task ReplyAsync(Message message)
        {
            var helper = new TimeoutHelper(_defaultSendTimeout);
            return ReplyAsync(message, helper.GetCancellationToken());
        }

        public override async Task ReplyAsync(Message message, CancellationToken token)
        {
            // "null" is a valid reply (signals a 202-style "ack"), so we don't have a null-check here
            lock (ThisLock)
            {
                ThrowIfInvalidReply();
                ReplyInitiated = true;
            }

            await OnReplyAsync(message, token);
            _replySent = true;
        }

        // This method is designed for WebSocket only, and will only be used once the WebSocket response was sent.
        // For WebSocket, we never call HttpRequestContext.Reply to send the response back. 
        // Instead we call AcceptWebSocket directly. So we need to set the replyInitiated and 
        // replySent boolean to be true once the response was sent successfully. Otherwise when we 
        // are disposing the HttpRequestContext, we will see a bunch of warnings in trace log.
        protected void SetReplySent()
        {
            lock (ThisLock)
            {
                ThrowIfInvalidReply();
                ReplyInitiated = true;
            }

            _replySent = true;
        }
    }

    internal class RequestContextMessageProperty : IDisposable
    {
        private RequestContext _context;
        private readonly object _thisLock = new object();

        public RequestContextMessageProperty(RequestContext context)
        {
            _context = context;
        }

        public static string Name
        {
            get { return "requestContext"; }
        }

        void IDisposable.Dispose()
        {
            bool success = false;
            RequestContext thisContext;

            lock (_thisLock)
            {
                if (_context == null)
                {
                    return;
                }

                thisContext = _context;
                _context = null;
            }

            try
            {
                thisContext.CloseAsync().GetAwaiter().GetResult();
                success = true;
            }
            catch (CommunicationException e)
            {
                DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
            }
            catch (TimeoutException e)
            {
                //if (TD.CloseTimeoutIsEnabled())
                //{
                //    TD.CloseTimeout(e.Message);
                //}
                DiagnosticUtility.TraceHandledException(e, TraceEventType.Information);
            }
            finally
            {
                if (!success)
                {
                    thisContext.Abort();
                }
            }
        }
    }
}
