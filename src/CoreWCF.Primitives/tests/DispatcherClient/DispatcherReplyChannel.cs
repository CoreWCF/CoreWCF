using System;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF;
using CoreWCF.Channels;
using Microsoft.Extensions.DependencyInjection;

namespace DispatcherClient
{
    internal class DispatcherReplyChannel : CommunicationObject, IReplyChannel
    {
        private IServiceProvider _serviceProvider;

        public DispatcherReplyChannel(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public EndpointAddress LocalAddress => _serviceProvider.GetRequiredService<EndpointAddress>();

        protected override TimeSpan DefaultCloseTimeout => _serviceProvider.GetRequiredService<DispatcherChannelFactory>().CloseTimeout;

        protected override TimeSpan DefaultOpenTimeout => _serviceProvider.GetRequiredService<DispatcherChannelFactory>().OpenTimeout;

        public T GetProperty<T>() where T : class
        {
            return _serviceProvider.GetService<T>();
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
    }
}