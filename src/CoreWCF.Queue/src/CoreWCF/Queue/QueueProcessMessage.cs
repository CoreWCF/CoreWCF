using System;
using CoreWCF.Configuration;
using System.Threading.Tasks;
using CoreWCF.Queue.Common.Configuration;

namespace CoreWCF.Queue.Common
{
    public class QueueProcessMessage
    {
        private readonly QueueMessageDispatcherDelegate _next;

        private QueueInputChannel _inputChannel;
        private IServiceChannelDispatcher _channelDispatcher;
        public QueueProcessMessage(QueueMessageDispatcherDelegate next)
        {
            _next = next;
            _inputChannel = new QueueInputChannel();
        }

        public async Task InvokeAsync(QueueMessageContext queueMessageContext)
        {
            if (_channelDispatcher == null)
                _channelDispatcher = await queueMessageContext.QueueTransportContext.ServiceDispatcher.CreateServiceChannelDispatcherAsync(_inputChannel);

               await  _channelDispatcher.DispatchAsync(queueMessageContext);

            //if success/failures, populate the message and notify the internal transport
        }
    }
}

