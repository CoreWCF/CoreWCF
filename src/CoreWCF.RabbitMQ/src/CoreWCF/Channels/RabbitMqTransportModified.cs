using System;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Channels;
using CoreWCF.Queue;
using CoreWCF.Queue.Common;
using RabbitMQ.Client.Events;

namespace CoreWCF.RabbitMQ.CoreWCF.Channels
{
    //TODO : Remove this file
    public class RabbitMqTransportModified //: IQueueTransport
    {
       /* public RabbitMqTransportModified()
        {
        }

        public Task<QueueMessageContext> NotifyAfterProcessed()
        {

        }
        public ValueTask<QueueMessageContext> ReceiveQueueMessageContextAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private async void ConsumeMessage(BasicDeliverEventArgs eventArgs)
        {
            _logger.LogInformation("Receiving message from rabbitmq");
            var reader = PipeReader.Create(new MemoryStream(eventArgs.Body.ToArray()));

            var queueUrl = $"soap.amqp://{_queueSettings.QueueName}";
            var messageContext =
                _rabbitMqConnectionHandler.GetContext(reader, queueUrl);
            var dispatch = messageContext.QueueTransportContext.QueueHandShakeDelegate;
            messageContext.Properties.Add("addressTo", new Uri(queueUrl));

            await dispatch(messageContext);
        }*/
    }
}

