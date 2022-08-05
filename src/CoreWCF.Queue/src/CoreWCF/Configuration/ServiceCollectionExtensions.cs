// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Queue;
using Microsoft.Extensions.DependencyInjection;

namespace CoreWCF.Configuration
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddServiceModelQueue(this IServiceCollection services,
            Action<QueueOptions> configureQueues)
        {
            services.AddTransient<IQueueMessagePipelineBuilder, QueueMessagePipelineBuilder>();

            services.Configure(configureQueues);

            services.AddSingleton(ReceiverFactory.Create);
            services.AddHostedService<QueueMessageReceiver>();

            return services;
        }
    }
}
