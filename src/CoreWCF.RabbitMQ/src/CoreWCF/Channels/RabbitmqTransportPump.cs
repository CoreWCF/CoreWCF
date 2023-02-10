// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.IO.Pipelines;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Channels.Configuration;
using CoreWCF.Configuration;
using CoreWCF.Queue.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CoreWCF.Channels
{
    internal class RabbitMqTransportPump : QueueTransportPump
    {
        private const bool DefaultAutoAck = false;
        private readonly ILogger<RabbitMqTransportPump> _logger;
        private SslOption _sslOption;
        private QueueDeclareConfiguration _queueConfiguration;
        private ICredentials _credentials;
        private string _virtualHost;
        private ConnectionFactory _factory;
        private IConnection _connection;
        private IModel _channel;
        private EventingBasicConsumer _consumer;
        private readonly Uri _baseAddress;
        private bool _isAutoAck;

        public RabbitMqTransportPump(
            IServiceProvider serviceProvider,
            IServiceDispatcher serviceDispatcher,
            SslOption sslOption,
            QueueDeclareConfiguration queueConfiguration,
            ICredentials credentials,
            string virtualHost)
        {
            _baseAddress = serviceDispatcher.BaseAddress;
            _logger = serviceProvider.GetRequiredService<ILogger<RabbitMqTransportPump>>();
            _sslOption = sslOption;
            _queueConfiguration = queueConfiguration;
            _credentials = credentials;
            _virtualHost = virtualHost;
            _isAutoAck = DefaultAutoAck;
        }

        public override Task StartPumpAsync(QueueTransportContext queueTransportContext, CancellationToken token)
        {
            var rabbitMqTransport = queueTransportContext.QueueBindingElement as RabbitMqTransportBindingElement;
            if (rabbitMqTransport == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(rabbitMqTransport));
            }

            var connectionSettings = RabbitMqConnectionSettings.FromUri(_baseAddress, _credentials, _sslOption, _virtualHost);
            _factory = connectionSettings.GetConnectionFactory();
            _connection = _factory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.BasicQos(0, _queueConfiguration.PrefetchCount, _queueConfiguration.GlobalQosPrefetch);
            _channel.QueueDeclare(
                connectionSettings.QueueName,
                _queueConfiguration.Durable,
                _queueConfiguration.Exclusive,
                _queueConfiguration.AutoDelete,
                _queueConfiguration.ToDictionary());
            _channel.QueueBind(
                connectionSettings.QueueName,
                connectionSettings.Exchange,
                connectionSettings.RoutingKey,
                null);

            _consumer = new EventingBasicConsumer(_channel);
            _consumer.Received += (_, ea) =>
            {
                ConsumeMessage(ea, queueTransportContext);
            };
            _channel.BasicConsume(connectionSettings.QueueName, DefaultAutoAck, _consumer);
            return Task.CompletedTask;
        }

        public override Task StopPumpAsync(CancellationToken token)
        {
            _channel?.Close();
            _channel?.Dispose();

            _connection?.Close();
            _connection?.Dispose();
            return Task.CompletedTask;
        }

        private async void ConsumeMessage(BasicDeliverEventArgs eventArgs, QueueTransportContext queueTransportContext)
        {
            _logger.LogInformation("Receiving message from RabbitMQ");
            var reader = PipeReader.Create(new ReadOnlySequence<byte>(eventArgs.Body));
            var deliveryTag = eventArgs.DeliveryTag;

            await queueTransportContext.QueueMessageDispatcher(GetContext(reader, queueTransportContext, deliveryTag));
        }

        private QueueMessageContext GetContext(PipeReader reader, QueueTransportContext transportContext, ulong deliveryTag)
        {
            var receiveContext = new RabbitMqReceiveContext(deliveryTag, _channel, _isAutoAck, _logger);
            var context = new QueueMessageContext
            {
                QueueMessageReader = reader,
                LocalAddress = new EndpointAddress(transportContext.ServiceDispatcher.BaseAddress),
                QueueTransportContext = transportContext,
                ReceiveContext = receiveContext
            };

            return context;
        }
    }
}
