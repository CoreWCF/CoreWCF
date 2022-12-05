// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using RabbitMQ.Client;

namespace CoreWCF.RabbitMQ.Tests.Helpers
{
    internal class MessageQueueHelper
    {
        public static void SendMessageInQueue()
        {
            var factory = new ConnectionFactory { HostName = "localhost" };
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();
            var message = MessageContainer.GetTestMessage();
            var memStream = new MemoryStream();
            message.CopyTo(memStream);
            // routing key begin with "/", for example: /hello
            channel.BasicPublish("amq.direct", $"/{IntegrationTests.QueueName}", null, memStream.ToArray());
        }
    }
}
