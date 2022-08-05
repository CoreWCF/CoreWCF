// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Configuration;
using CoreWCF.Queue;
using Microsoft.Extensions.Logging;

namespace CoreWCF.Channels
{
    public class RabbitMqTransportFactory : IQueueTransportFactory
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly IQueueConnectionHandler _rabbitMqConnectionHandler;

        public RabbitMqTransportFactory(
            ILoggerFactory loggerFactory,
            IQueueConnectionHandler rabbitMqConnectionHandler)
        {
            _loggerFactory = loggerFactory;
            _rabbitMqConnectionHandler = rabbitMqConnectionHandler;
        }

        public IQueueTransport Create(QueueSettings settings)
        {
            return new RabbitMqTransport(_loggerFactory, _rabbitMqConnectionHandler, settings);
        }
    }
}
