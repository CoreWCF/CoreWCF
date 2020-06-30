using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Diagnostics;
using CoreWCF.Runtime;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Dispatcher
{
    //For Security over SOAP, creating a separte Binder
    class SecurityReplyChannelBinder : IChannelBinder
    {
        private IReplyChannel _channel;
        private bool _initialized = false;
        private IServiceChannelDispatcher _next;
        private SecurityServiceDispatcher _securityDispatcher;

        public SecurityReplyChannelBinder() { }

        internal void Init(IReplyChannel channel, Uri listenUri)
        {
            if (_initialized)
            {
                Fx.Assert(_channel == channel, "Wrong channel when calling Init");
                Fx.Assert(ListenUri == listenUri, "Wrong listenUri when calling Init");
                return;
            }

            if (channel == null)
            {
                Fx.Assert("ReplyChannelBinder.ReplyChannelBinder: (channel != null)");
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(channel));
            }
            _channel = channel;
            ListenUri = listenUri;
            _initialized = true;
            if(_channel is SecurityReplyChannel)
            {
                this._securityDispatcher = ((SecurityReplyChannel)_channel).SecurityServiceDispatcher;
            }
        }

        public IChannel Channel
        {
            get { return _channel; }
        }

        public bool HasSession
        {
            get { return _channel is ISessionChannel<IInputSession>; }
        }

        public Uri ListenUri { get; private set; }

        public EndpointAddress LocalAddress
        {
            get { return _channel.LocalAddress; }
        }

        public EndpointAddress RemoteAddress
        {
            get
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotImplementedException());
            }
        }

        public void Abort()
        {
            _channel.Abort();
        }

        public void CloseAfterFault(TimeSpan timeout)
        {
            var helper = new TimeoutHelper(timeout);
            _channel.CloseAsync(helper.GetCancellationToken()).GetAwaiter().GetResult();
        }

        public RequestContext CreateRequestContext(Message message)
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotImplementedException());
        }

        public Task SendAsync(Message message, CancellationToken token)
        {
            throw TraceUtility.ThrowHelperError(new NotImplementedException(), message);
        }

        public Task<Message> RequestAsync(Message message, CancellationToken token)
        {
            throw TraceUtility.ThrowHelperError(new NotImplementedException(), message);
        }

        public void SetNextDispatcher(IServiceChannelDispatcher dispatcher)
        {
            _next = dispatcher;
        }

        public Task DispatchAsync(RequestContext context)
        {
            Fx.Assert(_next != null, "SetNextDispatcher wasn't called");
            context = HandleHandshake(context);
            //Add logic to separate flow for handshake or actual call
            return _next.DispatchAsync(context);
        }

        private SecurityRequestContext HandleHandshake(RequestContext context)
        {
            SecurityReplyChannel securityReplyChannel = (SecurityReplyChannel)_channel;
            SecurityRequestContext securedMessage = (SecurityRequestContext)securityReplyChannel.ProcessReceivedRequest(context);
            return securedMessage;
        }

        public Task DispatchAsync(Message message)
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotImplementedException());
        }
    }
}
