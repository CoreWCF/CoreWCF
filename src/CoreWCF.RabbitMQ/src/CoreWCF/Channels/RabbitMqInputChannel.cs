// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace CoreWCF.Channels
{
    internal class RabbitMqInputChannel : ServiceChannelBase, IInputChannel
    {
        private readonly IServiceProvider _serviceProvider;

        public RabbitMqInputChannel(IDefaultCommunicationTimeouts timeouts, IServiceProvider serviceProvider, EndpointAddress endpointAddress)
            : base(timeouts)
        {
            _serviceProvider = serviceProvider;
            LocalAddress = endpointAddress;
        }

        protected override void OnAbort()
        {
        }

        protected override Task OnCloseAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }

        protected override Task OnOpenAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }

        public EndpointAddress LocalAddress { get; }
        public Task<Message> ReceiveAsync(CancellationToken token) => throw new NotImplementedException();

        public Task<(Message message, bool success)> TryReceiveAsync(CancellationToken token) =>
            throw new NotImplementedException();

        public override T GetProperty<T>()
        {
            T service = _serviceProvider.GetService<T>();
            return service ?? base.GetProperty<T>();
        }
    }
}
