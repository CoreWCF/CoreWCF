﻿using System;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Runtime;
using CoreWCF.Channels;
using CoreWCF.Diagnostics;
using CoreWCF.Configuration;

namespace CoreWCF.Dispatcher
{
    internal class InputChannelBinder : IChannelBinder
    {
        private IInputChannel _channel;
        private IServiceChannelDispatcher _next;

        public InputChannelBinder()
        {

        }

        internal void Init(IInputChannel channel, Uri listenUri)
        {
            if (!((channel != null)))
            {
                Fx.Assert("InputChannelBinder.InputChannelBinder: (channel != null)");
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(channel));
            }
            _channel = channel;
            ListenUri = listenUri;
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
            return WrapMessage(message);
        }

        public Task SendAsync(Message message, CancellationToken token)
        {
            throw TraceUtility.ThrowHelperError(new NotImplementedException(), message);
        }

        public Task<Message> RequestAsync(Message message, CancellationToken token)
        {
            throw TraceUtility.ThrowHelperError(new NotImplementedException(), message);
        }

        private RequestContext WrapMessage(Message message)
        {
            if (message == null)
            {
                return null;
            }
            else
            {
                return new InputRequestContext(message, this);
            }
        }

        public void SetNextDispatcher(IServiceChannelDispatcher dispatcher)
        {
            _next = dispatcher;
        }

        public Task DispatchAsync(RequestContext context)
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotImplementedException());
        }

        public Task DispatchAsync(Message message)
        {
            var requestContext = WrapMessage(message);
            return _next.DispatchAsync(requestContext);
        }

        private class InputRequestContext : RequestContextBase
        {
            private InputChannelBinder binder;

            internal InputRequestContext(Message request, InputChannelBinder binder)
                : base(request, TimeSpan.Zero, TimeSpan.Zero)
            {
                this.binder = binder;
            }

            protected override void OnAbort()
            {
            }

            protected override Task OnCloseAsync(CancellationToken token)
            {
                return Task.CompletedTask;
            }

            protected override Task OnReplyAsync(Message message, CancellationToken token)
            {
                return Task.CompletedTask;
            }
        }
    }

}