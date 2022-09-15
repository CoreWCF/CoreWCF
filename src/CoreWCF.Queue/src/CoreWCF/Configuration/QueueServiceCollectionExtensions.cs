// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Queue;
using CoreWCF.Queue.CoreWCF.Queue;
using Microsoft.Extensions.DependencyInjection;

namespace CoreWCF.Queue.Common.Configuration
{
    public static class QueueServiceCollectionExtension
    {
        public static IServiceCollection AddQueueTransport(this IServiceCollection services,
            Action<QueueOptions> configureQueues = null)
        {
            services.AddTransient<IQueueMiddlewareBuilder, QueueMiddlewareBuilder>();
            if (configureQueues != null)
            {
                services.Configure(configureQueues);
            }
            services.AddSingleton<QueueHandShakeMiddleWare>();
            services.AddHostedService<QueuePollingService>();
            services.AddTransient<QueueInputChannel>();
            return services;

           /* services.Configure(configureQueues);

            services.AddSingleton(ReceiverFactory.Create);
            services.AddHostedService<QueueMessageReceiver>();

            return services;*/
        }
    }
}
