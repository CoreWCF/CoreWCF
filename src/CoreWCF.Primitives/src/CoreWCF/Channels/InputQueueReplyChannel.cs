// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    internal abstract class InputQueueReplyChannel : InputQueueServiceChannelDispatcher<RequestContext>, IReplyChannel
    {
        private IServiceChannelDispatcher _serviceChannelDispatcher;
        private IServiceDispatcher _serviceDispatcher;

        public InputQueueReplyChannel(IDefaultCommunicationTimeouts timeouts, IServiceDispatcher serviceDispatcher, EndpointAddress localAddress) : base(timeouts)
        {
            ReliableMessagingHelpers.AssertIsNotReliableServiceDispatcher(serviceDispatcher);
            LocalAddress = localAddress;
            _serviceDispatcher = serviceDispatcher;
        }

        public EndpointAddress LocalAddress { get; }

        protected AsyncLock AsyncLock = new AsyncLock();

        public override T GetProperty<T>()
        {
            if (typeof(T) == typeof(IReplyChannel))
            {
                return (T)(object)this;
            }

            T baseProperty = base.GetProperty<T>();
            if (baseProperty != null)
            {
                return baseProperty;
            }

            return default;
        }

        public override async Task InnerDispatchAsync(RequestContext context)
        {
            if (_serviceChannelDispatcher == null)
            {
                await using(await AsyncLock.TakeLockAsync())
                {
                    if (_serviceChannelDispatcher == null)
                    {
                        _serviceChannelDispatcher = await _serviceDispatcher.CreateServiceChannelDispatcherAsync(this);
                    }
                }
            }

            ThrowIfDisposedOrNotOpen();
            await _serviceChannelDispatcher.DispatchAsync(context);
        }

        protected override void OnAbort() { }
        protected override Task OnCloseAsync(CancellationToken token) => Task.CompletedTask;
        protected override Task OnOpenAsync(CancellationToken token) => Task.CompletedTask;

        public void Shutdown()
        {
            _ = InnerDispatchAsync((RequestContext)null);
        }
    }
}
