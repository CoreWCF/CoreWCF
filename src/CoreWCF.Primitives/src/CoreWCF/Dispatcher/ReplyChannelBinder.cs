// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Diagnostics;
using CoreWCF.Runtime;

namespace CoreWCF.Dispatcher
{
    internal class ReplyChannelBinder : IChannelBinder
    {
        private IReplyChannel _channel;
        private bool _initialized = false;
        private IServiceChannelDispatcher _next;

        public ReplyChannelBinder() { }

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
            return _next.DispatchAsync(context);
        }

        public Task DispatchAsync(Message message)
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotImplementedException());
        }
    }
}
