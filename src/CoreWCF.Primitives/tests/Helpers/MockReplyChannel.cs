using Microsoft.Extensions.DependencyInjection;
using CoreWCF;
using CoreWCF.Channels;
using System;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Configuration;

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
