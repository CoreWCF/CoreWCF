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
    public class MsmqFetchMessageMiddleware
    {
        private readonly QueueMessageDispatch _next;
        private readonly ILogger<MsmqFetchMessageMiddleware> _logger;
        private readonly IServiceScopeFactory _servicesScopeFactory;
        private readonly IDeadLetterQueueMsmqSender _deadLetterQueueSender;

        public MsmqFetchMessageMiddleware(
            QueueMessageDispatch next,
            ILogger<MsmqFetchMessageMiddleware> logger,
            IServiceScopeFactory servicesScopeFactory,
            IDeadLetterQueueMsmqSender deadLetterQueueSender)
        {
            _next = next;
            _logger = logger;
            _servicesScopeFactory = servicesScopeFactory;
            _deadLetterQueueSender = deadLetterQueueSender;
        }

        public async Task InvokeAsync(QueueMessageContext context)
        {
            _logger.LogInformation($"Invoke {nameof(MsmqFetchMessageMiddleware)}");

            try
            {
                var readResult = await context.Reader.ReadAsync();
                var memStream = new MemoryStream(readResult.Buffer.ToArray());
                int maxReceivedMessageSize =
                    (int)(context.QueueTransportContext.Binding as NetMsmqBinding).MaxReceivedMessageSize;
                var message = MsmqDecodeHelper.DecodeTransportDatagram(memStream,
                    context.QueueTransportContext.MessageEncoder, maxReceivedMessageSize);
                context.RequestMessage = message;

                if (context.QueueTransportContext.ServiceChannelDispatcher == null)
                {
                    var timeouts = new ImmutableCommunicationTimeouts();
                    var inputChannel = new MsmqInputChannel(timeouts,
                        _servicesScopeFactory.CreateScope().ServiceProvider, context.LocalAddress);
                    context.QueueTransportContext.ServiceChannelDispatcher =
                        await context.QueueTransportContext.ServiceDispatcher.CreateServiceChannelDispatcherAsync(
                            inputChannel);
                }

                await _next(context);
            }
            catch (Exception e)
            {
                _logger.LogError($"Error with processing msmq message, exception: {e}");

                if (context.QueueTransportContext.Binding is NetMsmqBinding binding &&
                    binding.DeadLetterQueue == DeadLetterQueue.Custom)
                {
                    await _deadLetterQueueSender.Send(context.Reader, binding.CustomDeadLetterQueue);
                }
                else
                {
                    await _deadLetterQueueSender.SendToSystem(context.Reader, context.LocalAddress.Uri);
                }
            }
        }
    }
}
