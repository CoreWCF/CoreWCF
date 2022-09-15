// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.IO.Pipelines;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using CoreWCF.Queue;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CoreWCF.Channels
{
    //TODO : Remove this file
    internal class RabbitMqTransport //: IQueueTransport
    {
        /*
        private readonly ILogger<RabbitMqTransport> _logger;
        private readonly IQueueConnectionHandler _rabbitMqConnectionHandler;
        private readonly QueueSettings _queueSettings;
        private ConnectionFactory _factory;
        private IConnection _connection;
        private IModel _channel;

        public RabbitMqTransport(
            ILoggerFactory loggerFactory,
            IQueueConnectionHandler rabbitMqConnectionHandler,
            QueueSettings queueSettings)
        {
            _logger = loggerFactory.CreateLogger<RabbitMqTransport>();
            _rabbitMqConnectionHandler = rabbitMqConnectionHandler;
            _queueSettings = queueSettings;
        }

        public Task StartAsync()
        {
            _factory = new ConnectionFactory { HostName = "localhost" };
            _connection = _factory.CreateConnection();
            _channel = _connection.CreateModel();

            //Create a queue for messages destined to this service, bind it to the service URI routing key
            string queue = _channel.QueueDeclare();
            var exchange = "amq.direct";
            _channel.QueueBind(queue, exchange, _queueSettings.QueueName, null); // /hello

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += (model, ea) => { ConsumeMessage(ea); };
            _channel.BasicConsume(queue, true, consumer);


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
        }

        public Task StopAsync()
        {
            _channel.Dispose();
            _connection.Dispose();
            return Task.CompletedTask;
        }
        */
    }
}
