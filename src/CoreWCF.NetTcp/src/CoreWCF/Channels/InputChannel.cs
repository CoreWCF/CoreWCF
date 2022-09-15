// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace CoreWCF.Channels
{
    internal class InputChannel : ServiceChannelBase, IInputChannel
    {
        private readonly IServiceProvider _serviceProvider;

        public InputChannel(ITransportFactorySettings settings, EndpointAddress localAddress, IServiceProvider serviceProvider) : base(settings)
        {
            LocalAddress = localAddress;
            _serviceProvider = serviceProvider;
        }

        public EndpointAddress LocalAddress { get; }


        public Task<Message> ReceiveAsync()
        {
            throw new NotImplementedException();
        }

        public Task<Message> ReceiveAsync(CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public Task<(Message message, bool success)> TryReceiveAsync(CancellationToken token)
        {
            throw new NotImplementedException();
        }

        protected override void OnAbort()
        {
            return;
        }

        protected override Task OnCloseAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }

        protected override Task OnOpenAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }

        public override T GetProperty<T>()
        {
            T service = _serviceProvider.GetService<T>();
            if (service != null)
            {
                return service;
            }

            return base.GetProperty<T>();
        }
    }
}
