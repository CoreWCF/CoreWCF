// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Helpers
{
    internal class MockReplyChannel : IReplyChannel
    {
        private IServiceScope _serviceScope;

        public MockReplyChannel(IServiceProvider serviceProvider)
        {
            var servicesScopeFactory = serviceProvider.GetRequiredService<IServiceScopeFactory>();
            _serviceScope = servicesScopeFactory.CreateScope();
        }

        public EndpointAddress LocalAddress => throw new NotImplementedException();

        public CommunicationState State { get; set; } = CommunicationState.Opened;
        public IServiceChannelDispatcher ChannelDispatcher { get; set; }

        // These are required to implement IReplyChannel
        public event EventHandler Closed;
        public event EventHandler Closing;
        public event EventHandler Faulted;
        public event EventHandler Opened;
        public event EventHandler Opening;

        public void Abort()
        {
            throw new NotImplementedException();
        }

        public Task CloseAsync()
        {
            return CloseAsync(CancellationToken.None);
        }

        public Task CloseAsync(CancellationToken token)
        {
            State = CommunicationState.Closed;
            return Task.CompletedTask;
        }

        public T GetProperty<T>() where T : class
        {
            return _serviceScope.ServiceProvider.GetService<T>();
        }

        public Task OpenAsync()
        {
            return OpenAsync(CancellationToken.None);
        }

        public Task OpenAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }
    }
}
