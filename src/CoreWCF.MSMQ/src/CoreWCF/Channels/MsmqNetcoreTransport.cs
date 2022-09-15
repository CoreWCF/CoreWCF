using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Dispatcher;
using CoreWCF.Queue.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MSMQ.Messaging;
using MSMQM = MSMQ.Messaging;


namespace CoreWCF.Channels
{
    public class MsmqNetcoreTransport : IQueueTransport, IDisposable
    {
        private readonly QueueOptions _queueOptions;
        private readonly Uri _baseAddress;
        private readonly MessageQueue _messageQueue;
        private readonly TimeSpan _queueReceiveTimeOut;

        public MsmqNetcoreTransport(QueueOptions options, IServiceDispatcher serviceDispatcher)
        {
            _baseAddress = serviceDispatcher.BaseAddress;
            _queueOptions = options;
            var nativeQueueName = MsmqQueueNameConverter.GetMsmqFormatQueueName(_queueOptions.QueueName);
            _messageQueue = new MessageQueue(nativeQueueName);
            _queueReceiveTimeOut = serviceDispatcher.Binding.ReceiveTimeout;
        }

        public async ValueTask<QueueMessageContext> ReceiveQueueMessageContextAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Func<IAsyncResult, MSMQM.Message> endDelegate = _messageQueue.EndReceive;
            var message = await Task.Factory.FromAsync<MessageQueue, TimeSpan, MSMQM.Message>(MessageQueueBeginReceive, endDelegate, _messageQueue, _queueReceiveTimeOut, null);
            var reader = PipeReader.Create(message.BodyStream);
            MsmqDecodeHelper.DecodeTransportDatagram(reader);
            return GetContext(reader, _baseAddress);
        }

        private QueueMessageContext GetContext(PipeReader reader, Uri uri)
        {
            var context = new QueueMessageContext
            {
                QueueMessageReader = reader,
                LocalAddress = new EndpointAddress(uri),
                Properties = new Dictionary<string, object>(),
                DispatchResultHandler = NotifyError,
            };
            return context;
        }

        private static IAsyncResult MessageQueueBeginReceive(MessageQueue messageQueue, TimeSpan timeout, AsyncCallback callback, object state)
        {
            return messageQueue.BeginReceive(timeout, state, callback);
        }

        //TODO : Wireup DeadLetter based on QueueDispatchResult.Failed
        private void NotifyError(QueueDispatchResult dispatchResult, QueueMessageContext queueMessageContext)
        {
           if(dispatchResult == QueueDispatchResult.Failed)
            {
                //DO Something
            }
        }

        public void Dispose()
        {
            _messageQueue.Close();
        }
    }
}

