using System;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Runtime;
using CoreWCF.Diagnostics;
using System.Diagnostics;

namespace CoreWCF.Channels
{
    internal abstract class RequestContextBase : RequestContext
    {
        TimeSpan defaultSendTimeout;
        TimeSpan defaultCloseTimeout;
        CommunicationState state = CommunicationState.Opened;
        Message requestMessage;
        Exception requestMessageException;
        bool replySent;
        bool replyInitiated;
        bool aborted;
        object thisLock = new object();

        protected RequestContextBase(Message requestMessage, TimeSpan defaultCloseTimeout, TimeSpan defaultSendTimeout)
        {
            this.defaultSendTimeout = defaultSendTimeout;
            this.defaultCloseTimeout = defaultCloseTimeout;
            this.requestMessage = requestMessage;
        }

        public void ReInitialize(Message requestMessage)
        {
            state = CommunicationState.Opened;
            requestMessageException = null;
            replySent = false;
            replyInitiated = false;
            aborted = false;
            this.requestMessage = requestMessage;
        }

        public override Message RequestMessage
        {
            get
            {
                if (requestMessageException != null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(requestMessageException);
                }

                return requestMessage;
            }
        }

        protected void SetRequestMessage(Message requestMessage)
        {
            Fx.Assert(requestMessageException == null, "Cannot have both a requestMessage and a requestException.");
            this.requestMessage = requestMessage;
        }

        protected void SetRequestMessage(Exception requestMessageException)
        {
            Fx.Assert(requestMessage == null, "Cannot have both a requestMessage and a requestException.");
            this.requestMessageException = requestMessageException;
        }

        protected bool ReplyInitiated
        {
            get { return replyInitiated; }
        }

        protected object ThisLock
        {
            get
            {
                return thisLock;
            }
        }

        public bool Aborted
        {
            get
            {
                return aborted;
            }
        }

        public TimeSpan DefaultCloseTimeout
        {
            get { return defaultCloseTimeout; }
        }

        public TimeSpan DefaultSendTimeout
        {
            get { return defaultSendTimeout; }
        }

        public override void Abort()
        {
            lock (ThisLock)
            {
                if (state == CommunicationState.Closed)
                    return;

                state = CommunicationState.Closing;

                aborted = true;
            }

            //if (DiagnosticUtility.ShouldTraceWarning)
            //{
            //    TraceUtility.TraceEvent(TraceEventType.Warning, TraceCode.RequestContextAbort,
            //        SR.Format(SR.TraceCodeRequestContextAbort), this);
            //}

            try
            {
                OnAbort();
            }
            finally
            {
                state = CommunicationState.Closed;
            }
        }

        public override Task CloseAsync()
        {
            var helper = new TimeoutHelper(defaultCloseTimeout);
            return CloseAsync(helper.GetCancellationToken());
        }

        public override async Task CloseAsync(CancellationToken token)
        {
            bool sendAck = false;
            lock (ThisLock)
            {
                if (state != CommunicationState.Opened)
                    return;

                if (TryInitiateReply())
                {
                    sendAck = true;
                }

                state = CommunicationState.Closing;
            }

            bool throwing = true;

            try
            {
                if (sendAck)
                {
                    await OnReplyAsync(null, token);
                }

                await OnCloseAsync(token);
                state = CommunicationState.Closed;
                throwing = false;
            }
            finally
            {
                if (throwing)
                    Abort();
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (!disposing)
                return;

            if (replySent)
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
            if (state == CommunicationState.Closed || state == CommunicationState.Closing)
            {
                if (aborted)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CommunicationObjectAbortedException(SR.RequestContextAborted));
                else
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ObjectDisposedException(GetType().FullName));
            }

            if (replyInitiated)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.ReplyAlreadySent));
        }

        /// <summary>
        /// Attempts to initiate the reply. If a reply is not initiated already (and the object is opened), 
        /// then it initiates the reply and returns true. Otherwise, it returns false.
        /// </summary>
        protected bool TryInitiateReply()
        {
            lock (thisLock)
            {
                if ((state != CommunicationState.Opened) || replyInitiated)
                {
                    return false;
                }
                else
                {
                    replyInitiated = true;
                    return true;
                }
            }
        }

        public override Task ReplyAsync(Message message)
        {
            var helper = new TimeoutHelper(defaultSendTimeout);
            return ReplyAsync(message, helper.GetCancellationToken());
        }

        public override async Task ReplyAsync(Message message, CancellationToken token)
        {
            // "null" is a valid reply (signals a 202-style "ack"), so we don't have a null-check here
            lock (thisLock)
            {
                ThrowIfInvalidReply();
                replyInitiated = true;
            }

            await OnReplyAsync(message, token);
            replySent = true;
        }

        // This method is designed for WebSocket only, and will only be used once the WebSocket response was sent.
        // For WebSocket, we never call HttpRequestContext.Reply to send the response back. 
        // Instead we call AcceptWebSocket directly. So we need to set the replyInitiated and 
        // replySent boolean to be true once the response was sent successfully. Otherwise when we 
        // are disposing the HttpRequestContext, we will see a bunch of warnings in trace log.
        protected void SetReplySent()
        {
            lock (thisLock)
            {
                ThrowIfInvalidReply();
                replyInitiated = true;
            }

            replySent = true;
        }
    }

    class RequestContextMessageProperty : IDisposable
    {
        RequestContext context;
        object thisLock = new object();

        public RequestContextMessageProperty(RequestContext context)
        {
            this.context = context;
        }

        public static string Name
        {
            get { return "requestContext"; }
        }

        void IDisposable.Dispose()
        {
            bool success = false;
            RequestContext thisContext;

            lock (thisLock)
            {
                if (context == null)
                    return;
                thisContext = context;
                context = null;
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