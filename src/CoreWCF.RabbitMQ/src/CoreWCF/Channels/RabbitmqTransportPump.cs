// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using CoreWCF.Queue.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CoreWCF.Channels
{
    public class RabbitMqTransportPump : QueueTransportPump
    {
        private readonly ILogger<RabbitMqTransportPump> _logger;
        private ConnectionFactory _factory;
        private IConnection _connection;
        private IModel _channel;
        private EventingBasicConsumer _consumer;
        private readonly Uri _baseAddress;
        private const string Exchange = "amq.direct";

        public RabbitMqTransportPump(IServiceProvider serviceProvider, IServiceDispatcher serviceDispatcher)
        {
            _baseAddress = serviceDispatcher.BaseAddress;
            _logger = serviceProvider.GetRequiredService<ILogger<RabbitMqTransportPump>>();
        }

        public override Task StartPumpAsync(QueueTransportContext queueTransportContext, CancellationToken token)
        {
            _factory = new ConnectionFactory { HostName = _baseAddress.Host, Port = _baseAddress.Port };
            _connection = _factory.CreateConnection();
            _channel = _connection.CreateModel();

            //Create a queue for messages destined to this service, bind it to the service URI routing key
            string queue = _channel.QueueDeclare();
          
            // routing key begin with "/", for example: /hello
            _channel.QueueBind(queue, Exchange, _baseAddress.LocalPath, null);

            _consumer = new EventingBasicConsumer(_channel);
            _consumer.Received += (_, ea) => { ConsumeMessage(ea, queueTransportContext); };
            _channel.BasicConsume(queue, true, _consumer);

            return Task.CompletedTask;
        }

        public override Task StopPumpAsync(CancellationToken token)
        {
            _channel?.Dispose();
            _connection?.Dispose();
            return Task.CompletedTask;
        }

        private async void ConsumeMessage(BasicDeliverEventArgs eventArgs, QueueTransportContext queueTransportContext)
        {
            _logger.LogInformation("Receiving message from rabbitmq");
            var reader = PipeReader.Create(new ReadOnlySequence<byte>(eventArgs.Body));

            await queueTransportContext.QueueMessageDispatcher(GetContext(reader, queueTransportContext));
        }

        private QueueMessageContext GetContext(PipeReader reader, QueueTransportContext transportContext)
        {
            var context = new QueueMessageContext
            {
                QueueMessageReader = reader,
                LocalAddress = new EndpointAddress(transportContext.ServiceDispatcher.BaseAddress),
                QueueTransportContext = transportContext,
            };
            return context;
        }
    }
}
