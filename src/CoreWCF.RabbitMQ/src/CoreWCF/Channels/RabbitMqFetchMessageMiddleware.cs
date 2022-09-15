// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.IO;
using System.Threading.Tasks;
using CoreWCF.Queue;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CoreWCF.Channels
{
    //TODO :- Remove this file
    internal class RabbitMqFetchMessageMiddleware
    {
        /*
        private readonly QueueMessageDispatcherDelegate _next;
        private readonly ILogger<RabbitMqFetchMessageMiddleware> _logger;
        private readonly IServiceScopeFactory _servicesScopeFactory;

        public RabbitMqFetchMessageMiddleware(
            IServiceScopeFactory servicesScopeFactory,
            ILogger<RabbitMqFetchMessageMiddleware> logger,
            QueueMessageDispatcherDelegate next)
        {
            _servicesScopeFactory = servicesScopeFactory;
            _logger = logger;
            _next = next;
        }

        public async Task InvokeAsync(QueueMessageContext context)
        {
            _logger.LogInformation($"Invoke {nameof(RabbitMqFetchMessageMiddleware)}");

            var readResult = await context.Reader.ReadAsync();
            var memStream = new MemoryStream(readResult.Buffer.ToArray());
            var encoder = context.QueueTransportContext.MessageEncoder;
            var maxReceivedMessageSize = (int)(context.QueueTransportContext.Binding as RabbitMqBinding).MaxMessageSize;
            var message = await encoder.ReadMessageAsync(memStream, maxReceivedMessageSize); // todo m_bindingElement.MaxReceivedMessageSize
            message.Headers.To = context.Properties["addressTo"] as Uri;
            context.RequestMessage = message;

            if (context.QueueTransportContext.ServiceChannelDispatcher == null)
            {
                var timeouts = new ImmutableCommunicationTimeouts();
                var inputChannel = new RabbitMqInputChannel(timeouts, _servicesScopeFactory.CreateScope().ServiceProvider, context.LocalAddress);
                context.QueueTransportContext.ServiceChannelDispatcher =
                    await context.QueueTransportContext.ServiceDispatcher.CreateServiceChannelDispatcherAsync(inputChannel);
            }

            await _next(context);
        }
        */
    }
}
