// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Threading.Tasks;
using MSMQ.Messaging;

namespace CoreWCF.Channels
{
    internal class DeadLetterQueueSender : IDeadLetterQueueMsmqSender
    {
        public async Task Send(PipeReader message, Uri endpoint)
        {
            string nativeQueueName = MsmqQueueNameConverter.GetMsmqFormatQueueName(endpoint);
            if (!MessageQueue.Exists(nativeQueueName))
            {
                MessageQueue.Create(nativeQueueName);
            }
            MemoryStream memStream = await ConvertToStream(message);
            var queue = new MessageQueue(nativeQueueName);
            var messageForQueue = new MSMQ.Messaging.Message { BodyStream = memStream };
            queue.Send(messageForQueue);
        }

        public async Task SendToSystem(PipeReader message, Uri endpoint)
        {
            string nativeQueueName = MsmqQueueNameConverter.GetMsmqFormatQueueName(endpoint);
            MemoryStream memStream = await ConvertToStream(message);
            var queue = new MessageQueue(nativeQueueName);
            var messageForQueue = new MSMQ.Messaging.Message
            {
                BodyStream = memStream, UseDeadLetterQueue = true, TimeToBeReceived = TimeSpan.FromSeconds(0),
            };
            queue.Send(messageForQueue);
        }

        private static async Task<MemoryStream> ConvertToStream(PipeReader stream)
        {
            var readResult = await stream.ReadAsync();
            var memStream = new MemoryStream(readResult.Buffer.ToArray());
            return memStream;
        }
    }
}
