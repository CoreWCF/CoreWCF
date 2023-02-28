// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using CoreWCF.Channels;
using RabbitMQ.Client;

namespace CoreWCF.RabbitMQ.Tests.Helpers
{
    internal class MessageQueueHelper
    {
        public static void SendMessageToQueue(RabbitMqConnectionSettings connectionSettings)
        {
            var factory = connectionSettings.GetConnectionFactory();
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            var message = MessageContainer.GetTestMessage();
            var memStream = new MemoryStream();
            message.CopyTo(memStream);
            channel.BasicPublish(connectionSettings.Exchange, connectionSettings.RoutingKey, null, memStream.ToArray());
        }
    }
}
