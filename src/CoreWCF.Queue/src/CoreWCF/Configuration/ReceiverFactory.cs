using System;
using System.Collections.Generic;
using CoreWCF.Queue;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CoreWCF.Configuration
{
    internal static class ReceiverFactory
    {
        public static IEnumerable<IQueueTransport> Create(IServiceProvider serviceProvider)
        {
            var transports = new List<IQueueTransport>();
            var options = serviceProvider.GetRequiredService<IOptions<QueueOptions>>();
            foreach (QueueSettings queueOptions in options.Value.Queues)
            {
                var factory = serviceProvider.GetRequiredService<IQueueTransportFactory>();
                transports.Add(factory.Create(queueOptions));
            }

            return transports;
        }
    }
}
