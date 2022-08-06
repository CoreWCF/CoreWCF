// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CoreWCF.Queue
{
    public class QueueMessageReceiver : IHostedService
    {
        private readonly ILogger<QueueMessageReceiver> _logger;
        private readonly IEnumerable<IQueueTransport> _queueTransports;

        public QueueMessageReceiver(ILogger<QueueMessageReceiver> logger, IEnumerable<IQueueTransport> queueTransports)
        {
            _logger = logger;
            _queueTransports = queueTransports;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Start QueueMessageReceiver");
            foreach (var queueTransport in _queueTransports)
            {
                await queueTransport.StartAsync();
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stop QueueMessageReceiver");
            foreach (var queueTransport in _queueTransports)
            {
                await queueTransport.StopAsync();
            }
        }
    }
}
