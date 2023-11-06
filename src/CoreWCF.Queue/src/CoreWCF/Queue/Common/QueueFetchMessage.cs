// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.IO;
using System.Threading.Tasks;
using CoreWCF.Queue.Common.Configuration;

namespace CoreWCF.Queue.Common
{
    internal class QueueFetchMessage
    {
        private readonly QueueMessageDispatcherDelegate _next;

        public QueueFetchMessage(QueueMessageDispatcherDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(QueueMessageContext queueMessageContext)
        {
            try
            {
                var pipeReader = queueMessageContext.QueueMessageReader;
                var readResult = await pipeReader.ReadAsync();
                var memStream = new MemoryStream(readResult.Buffer.ToArray());
                var encoder = queueMessageContext.QueueTransportContext.MessageEncoderFactory.Encoder;
                var maxReceivedMessageSize =
                    queueMessageContext.QueueTransportContext.QueueBindingElement.MaxReceivedMessageSize;
                var message = await encoder.ReadMessageAsync(memStream, (int)maxReceivedMessageSize);
                if (message.Headers.To == null)
                {
                    message.Headers.To = queueMessageContext.LocalAddress.Uri;
                }
                foreach (var property in queueMessageContext.Properties)
                {
                    message.Properties.Add(property.Key, property.Value);
                }

                queueMessageContext.SetRequestMessage(message);
            }
            catch (Exception e)
            {
                queueMessageContext.SetRequestMessage(e);
            }
            await _next(queueMessageContext);
        }
    }
}
