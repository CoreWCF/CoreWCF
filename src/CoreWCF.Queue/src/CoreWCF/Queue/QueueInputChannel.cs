using System;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace CoreWCF.Queue.Common
{
    public class QueueInputChannel : CommunicationObject, IInputChannel
    {
        IServiceProvider _serviceProvider;
        public QueueInputChannel(IServiceProvider provider)
        {
           IServiceScope serviceScope =  provider.CreateScope();
            _serviceProvider = serviceScope.ServiceProvider;
        }
        public IServiceChannelDispatcher ChannelDispatcher { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }


        public EndpointAddress LocalAddress
        {
            get; set;
        }

        protected override TimeSpan DefaultCloseTimeout { get { return TimeSpan.MaxValue; } }

        protected override TimeSpan DefaultOpenTimeout { get { return TimeSpan.MaxValue; } }

        public virtual T GetProperty<T>() where T : class {
            return _serviceProvider.GetService<T>();
        }
        public Task<Message> ReceiveAsync(CancellationToken token) => throw new NotImplementedException();
        public Task<(Message message, bool success)> TryReceiveAsync(CancellationToken token) => throw new NotImplementedException();
        protected override void OnAbort() { }
        protected override Task OnCloseAsync(CancellationToken token) { return Task.CompletedTask; }
        protected override Task OnOpenAsync(CancellationToken token) { return Task.CompletedTask; }
    }
}

