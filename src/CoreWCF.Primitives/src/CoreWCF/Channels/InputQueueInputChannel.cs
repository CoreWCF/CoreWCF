// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    internal abstract class InputQueueInputChannel : InputQueueServiceChannelDispatcher<Message>, IInputChannel
    {
        private IServiceChannelDispatcher _serviceChannelDispatcher;
        private Task<IServiceChannelDispatcher> _serviceChannelDispatcherCreateTask = null;

        public InputQueueInputChannel(IDefaultCommunicationTimeouts timeouts, IServiceDispatcher serviceDispatcher, EndpointAddress localAddress)
            : base(timeouts)
        {
            LocalAddress = localAddress;
            _serviceChannelDispatcherCreateTask = serviceDispatcher.CreateServiceChannelDispatcherAsync(this);
        }

        public EndpointAddress LocalAddress { get; }

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

        public override async Task InnerDispatchAsync(RequestContext context)
        {
            if (_serviceChannelDispatcher == null)
            {
                var createDispatcherTask = _serviceChannelDispatcherCreateTask;
                if (createDispatcherTask != null)
                {
                    _serviceChannelDispatcher = await createDispatcherTask;
                    _serviceChannelDispatcherCreateTask = null;
                }

                Fx.Assert(_serviceChannelDispatcher != null, "_serviceChannelDispatcher must not be null if _serviceChannelDispatcherCreateTask is null");
            }

            await _serviceChannelDispatcher.DispatchAsync(context);
        }

        public override async Task InnerDispatchAsync(Message message)
        {
            if (_serviceChannelDispatcher == null)
            {
                var createDispatcherTask = _serviceChannelDispatcherCreateTask;
                if (createDispatcherTask != null)
                {
                    _serviceChannelDispatcher = await createDispatcherTask;
                    _serviceChannelDispatcherCreateTask = null;
                }

                Fx.Assert(_serviceChannelDispatcher != null, "_serviceChannelDispatcher must not be null if _serviceChannelDispatcherCreateTask is null");
            }

            await _serviceChannelDispatcher.DispatchAsync(message);
        }
    }
}
