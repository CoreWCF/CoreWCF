using System;
using System.Buffers;
using System.IO;
using System.Threading.Tasks;
using CoreWCF.Channels;
using CoreWCF.Queue.Common.Configuration;

namespace CoreWCF.Queue.Common
{
    public class QueueFetchMessage
    {
        public readonly QueueMessageDispatcherDelegate _next;

        public QueueFetchMessage(QueueMessageDispatcherDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(QueueMessageContext queueMessageContext)
        {
            var pipereader = queueMessageContext.QueueMessageReader;
            var readResult = await pipereader.ReadAsync();
            var memStream = new MemoryStream(readResult.Buffer.ToArray());
            var encoder = queueMessageContext.QueueTransportContext.MessageEncoderFactory.Encoder;
            var maxReceivedMessageSize = queueMessageContext.QueueTransportContext.QueueBindingElement.MaxReceivedMessageSize;
            var message = await encoder.ReadMessageAsync(memStream, (int)maxReceivedMessageSize); 
            message.Headers.To = queueMessageContext.LocalAddress.Uri;
            queueMessageContext.SetRequestMessage(message);
            await _next(queueMessageContext);
        }
    }
}

