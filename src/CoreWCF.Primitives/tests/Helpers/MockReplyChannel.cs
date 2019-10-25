using Microsoft.Extensions.DependencyInjection;
using CoreWCF;
using CoreWCF.Channels;
using System;
using System.Threading;
using System.Threading.Tasks;

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

#pragma warning disable CS0067 // "The event is never used"
        // These are required to implement IReplyChannel
        public event EventHandler Closed;
        public event EventHandler Closing;
        public event EventHandler Faulted;
        public event EventHandler Opened;
        public event EventHandler Opening;
#pragma warning restore CS0067

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
            throw new NotImplementedException();
        }

        public Task OpenAsync(CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public Task<RequestContext> ReceiveRequestAsync()
        {
            throw new NotImplementedException();
        }

        public Task<RequestContext> ReceiveRequestAsync(CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public Task<TryAsyncResult<RequestContext>> TryReceiveRequestAsync(CancellationToken token)
        {
            throw new NotImplementedException();
        }

        public Task<bool> WaitForRequestAsync(CancellationToken token)
        {
            throw new NotImplementedException();
        }
    }
}
