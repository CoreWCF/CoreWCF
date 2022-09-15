using System;
using CoreWCF.Configuration;
using System.Threading.Tasks;
using CoreWCF.Queue.Common.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CoreWCF.Queue.Common
{
    public class QueueProcessMessage
    {
        private readonly QueueMessageDispatcherDelegate _next;
        private readonly IServiceProvider _serviceProvider;
        public QueueProcessMessage(QueueMessageDispatcherDelegate next, IServiceProvider serviceProvider)
        {
            _next = next;
            _serviceProvider = serviceProvider;
        }

        public async Task InvokeAsync(QueueMessageContext queueMessageContext)
        {
            QueueInputChannel inputChannel = _serviceProvider.GetRequiredService<QueueInputChannel>();
            inputChannel.LocalAddress =  new EndpointAddress(queueMessageContext.QueueTransportContext.ServiceDispatcher.BaseAddress);
            //await inputChannel.OpenAsync();
            var _channelDispatcher = await queueMessageContext.QueueTransportContext.ServiceDispatcher.CreateServiceChannelDispatcherAsync(inputChannel);
            await  _channelDispatcher.DispatchAsync(queueMessageContext);

            //if success/failures, populate the message and notify the internal transport
        }
    }
}

