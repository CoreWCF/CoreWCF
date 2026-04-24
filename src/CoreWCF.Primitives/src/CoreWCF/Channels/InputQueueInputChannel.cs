// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Configuration;

namespace CoreWCF.Channels
{
    internal abstract class InputQueueInputChannel : InputQueueServiceChannelDispatcher<Message>, IInputChannel
    {
        private IServiceChannelDispatcher _serviceChannelDispatcher;

        public InputQueueInputChannel(IDefaultCommunicationTimeouts timeouts, IServiceDispatcher serviceDispatcher, EndpointAddress localAddress)
            : base(timeouts)
        {
            ReliableMessagingHelpers.AssertIsNotReliableServiceDispatcher(serviceDispatcher);
            LocalAddress = localAddress;
            _serviceDispatcher = serviceDispatcher;
        }

        public EndpointAddress LocalAddress { get; }

        private IServiceDispatcher _serviceDispatcher;

        public override T GetProperty<T>()
        {
            if (typeof(T) == typeof(IInputChannel))
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

        protected override void OnAbort() { }
        protected override Task OnCloseAsync(CancellationToken token) => Task.CompletedTask;
        protected override Task OnOpenAsync(CancellationToken token) => Task.CompletedTask;
        public Task<Message> ReceiveAsync(CancellationToken token) => throw new NotImplementedException();
        public Task<(Message message, bool success)> TryReceiveAsync(CancellationToken token) => throw new NotImplementedException();

        public override Task InnerDispatchAsync(Message message)
        {
            ThrowIfDisposedOrNotOpen();
            return _serviceChannelDispatcher.DispatchAsync(message);
        }
    }
}
