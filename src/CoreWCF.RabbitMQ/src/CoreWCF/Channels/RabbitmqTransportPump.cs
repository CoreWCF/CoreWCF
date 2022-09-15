using System;
using System.Collections.Generic;
using System.Data.Common;
using System.IO;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using CoreWCF.Channels;
using CoreWCF.Queue;
using CoreWCF.Queue.Common;
using CoreWCF.Queue.Common.Configuration;
using CoreWCF.Queue.CoreWCF.Queue;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CoreWCF.RabbitMQ.CoreWCF.Channels
{
    //FYI : - New file added
    public class RabbitMqTransportPump : QueueTransportPump
    {
        private readonly ILogger<RabbitMqTransportPump> _logger;
        IOptions<QueueOptions> _queueOptions;
        private ConnectionFactory _factory;
        private IConnection _connection;
        private IModel _channel;

        public RabbitMqTransportPump(ILogger<RabbitMqTransportPump> logger, IOptions<QueueOptions> options)
        {
            _logger = logger;
            _queueOptions = options;
        }

        public override Task StartPumpAsync(QueueTransportContext queueTransportContext, CancellationToken token)
        {
            _factory = new ConnectionFactory { HostName = "localhost" };
            _connection = _factory.CreateConnection();
            _channel = _connection.CreateModel();

            //Create a queue for messages destined to this service, bind it to the service URI routing key
            string queue = _channel.QueueDeclare();
            var exchange = "amq.direct";
            _channel.QueueBind(queue, exchange, _queueOptions.Value.QueueName, null); // /hello

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += (model, ea) => { ConsumeMessage(ea, queueTransportContext); };
            _channel.BasicConsume(queue, true, consumer);


            return Task.CompletedTask;
        }

        public override Task StopPumpAsync(CancellationToken token)
        {
            _channel.Dispose();
            _connection.Dispose();
            return Task.CompletedTask;
        }
        private async void ConsumeMessage(BasicDeliverEventArgs eventArgs, QueueTransportContext queueTransportContext)
        {
            _logger.LogInformation("Receiving message from rabbitmq");
            var reader = PipeReader.Create(new MemoryStream(eventArgs.Body.ToArray()));

            var queueUrl = $"soap.amqp://{_queueOptions.Value.QueueName}";
            await queueTransportContext.QueueHandShakeDelegate(GetContext(reader, queueTransportContext));
        }

        private QueueMessageContext GetContext(PipeReader reader, QueueTransportContext transportContext)
        {
            var context = new QueueMessageContext
            {
                QueueMessageReader = reader,
                LocalAddress = new EndpointAddress(transportContext.ServiceDispatcher.BaseAddress),
                QueueTransportContext = transportContext,
                Properties = new Dictionary<string, object>(),
            };
            return context;
        }
    }
}

