// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Configuration;
using CoreWCF.Queue;
using Microsoft.Extensions.Logging;

namespace CoreWCF.Channels
{
    //TODO : Remove this file
    public class MsmqTransportFactory //: IQueueTransportFactory
    {
        /*
        private readonly ILoggerFactory _loggerFactory;
        private readonly IQueueConnectionHandler _msmqConnectionHandler;
        private readonly IServiceBuilder _serviceBuilder;

        public MsmqTransportFactory(
            ILoggerFactory loggerFactory,
            IQueueConnectionHandler msmqConnectionHandler,
            IServiceBuilder serviceBuilder)
        {
            _loggerFactory = loggerFactory;
            _msmqConnectionHandler = msmqConnectionHandler;
            _serviceBuilder = serviceBuilder;
        }

        public IQueueTransport Create(QueueOptions settings)
        {
            var transport = new MsmqNetCoreTransport(_loggerFactory, settings, _msmqConnectionHandler, _serviceBuilder);
            return transport;
        }*/
    }
}
