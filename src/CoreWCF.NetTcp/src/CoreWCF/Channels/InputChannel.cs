using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Channels
{
    class InputChannel : ServiceChannelBase, IInputChannel
    {
        private IServiceProvider _serviceProvider;

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

        public Task<bool> WaitForMessageAsync(CancellationToken token)
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
