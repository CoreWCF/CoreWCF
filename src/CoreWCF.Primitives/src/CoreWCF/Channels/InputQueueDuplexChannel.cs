// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using CoreWCF.Dispatcher;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    internal abstract class InputQueueDuplexChannel : InputQueueServiceChannelDispatcher<Message>, IDuplexChannel
    {
        private readonly EndpointAddress _localAddress;
        private IServiceChannelDispatcher _serviceChannelDispatcher;
        private IServiceDispatcher _serviceDispatcher;


        protected InputQueueDuplexChannel(IDefaultCommunicationTimeouts timeouts, IServiceDispatcher serviceDispatcher, EndpointAddress localAddress)
            : base(timeouts)
        {
            LocalAddress = localAddress;
            _serviceDispatcher = serviceDispatcher;
        }

        public virtual EndpointAddress LocalAddress { get; }

        public abstract EndpointAddress RemoteAddress { get; }
        public abstract Uri Via { get; }

        public override T GetProperty<T>()
        {
            if (typeof(T) == typeof(IDuplexChannel))
            {
                return (T)(object)this;
            }

            T baseProperty = base.GetProperty<T>();
            if (baseProperty != null)
            {
                return baseProperty;
            }

            return default(T);
        }

        protected abstract Task OnSendAsync(Message message, CancellationToken token);

        public Task SendAsync(Message message)
        {
            return SendAsync(message, TimeoutHelper.GetCancellationToken(DefaultSendTimeout));
        }

        public Task SendAsync(Message message, CancellationToken token)
        {
            if (message == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));

            ThrowIfDisposedOrNotOpen();

            AddHeadersTo(message);
            return OnSendAsync(message, token);
        }

        protected virtual void AddHeadersTo(Message message) { }

        public Task<Message> ReceiveAsync(CancellationToken token) => throw new NotImplementedException();
        public Task<(Message message, bool success)> TryReceiveAsync(CancellationToken token) => throw new NotImplementedException();
        protected override void OnAbort() { }
        protected override Task OnCloseAsync(CancellationToken token) => Task.CompletedTask;

        protected override async Task OnOpenAsync(CancellationToken token)
        {
            _serviceChannelDispatcher = await _serviceDispatcher.CreateServiceChannelDispatcherAsync(this);
        }

        public override Task InnerDispatchAsync(RequestContext context)
        {
            ThrowIfDisposedOrNotOpen();
            return _serviceChannelDispatcher.DispatchAsync(context);
        }

        public override Task InnerDispatchAsync(Message message)
        {
            ThrowIfDisposedOrNotOpen();
            return _serviceChannelDispatcher.DispatchAsync(message);
        }
    }
}
