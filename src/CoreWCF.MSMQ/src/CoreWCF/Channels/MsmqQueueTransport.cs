// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using CoreWCF.Queue.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MSMQ.Messaging;
using MSMQM = MSMQ.Messaging;

namespace CoreWCF.Channels
{
    public class MsmqQueueTransport : IQueueTransport, IDisposable
    {
        private readonly Uri _baseAddress;
        private readonly MessageQueue _messageQueue;
        private readonly TimeSpan _queueReceiveTimeOut;
        private readonly IDeadLetterQueueMsmqSender _deadLetterQueueSender;
        private readonly ILogger<MsmqQueueTransport> _logger;

        public MsmqQueueTransport(IServiceDispatcher serviceDispatcher, IServiceProvider serviceProvider)
        {
            _deadLetterQueueSender = serviceProvider.GetRequiredService<IDeadLetterQueueMsmqSender>();
            _baseAddress = serviceDispatcher.BaseAddress;
            string nativeQueueName = MsmqQueueNameConverter.GetMsmqFormatQueueName(_baseAddress);
            _messageQueue = new MessageQueue(nativeQueueName);
            _queueReceiveTimeOut = serviceDispatcher.Binding.ReceiveTimeout;
            _logger = serviceProvider.GetRequiredService<ILogger<MsmqQueueTransport>>();
        }

        public int ConcurrencyLevel => 1;

        public async ValueTask<QueueMessageContext> ReceiveQueueMessageContextAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _logger.LogInformation("Receiving message from msmq");
            var message = await Task.Factory.FromAsync(MessageQueueBeginReceive,
                MessageQueueEndReceive, _messageQueue, _queueReceiveTimeOut, null);

            if(message == null)
                return null;

            var reader = PipeReader.Create(message.BodyStream);
            try
            {
                await MsmqDecodeHelper.DecodeTransportDatagram(reader);
            }
            catch (MsmqPoisonMessageException)
            {
                await _deadLetterQueueSender.SendToSystem(reader, _baseAddress);
                return null;
            }

            return GetContext(reader, _baseAddress);
        }

        private MSMQM.Message MessageQueueEndReceive(IAsyncResult result)
        {
            MSMQM.Message message = null;
            try
            {
                message = _messageQueue.EndReceive(result);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            return message;
        }

        private QueueMessageContext GetContext(PipeReader reader, Uri uri)
        {
            var context = new QueueMessageContext
            {
                QueueMessageReader = reader,
                LocalAddress = new EndpointAddress(uri)
            };
            var receiveContext = new MsmqReceiveContext(context, _deadLetterQueueSender);
            context.ReceiveContext = receiveContext;

            return context;
        }

        private static IAsyncResult MessageQueueBeginReceive(MessageQueue messageQueue, TimeSpan timeout,
            AsyncCallback callback, object state)
        {
            return messageQueue.BeginReceive(timeout, state, callback);
        }

        public void Dispose()
        {
            _messageQueue.Close();
        }
    }
}
