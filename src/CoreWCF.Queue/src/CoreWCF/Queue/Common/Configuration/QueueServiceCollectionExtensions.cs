﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Extensions.DependencyInjection;

namespace CoreWCF.Queue.Common.Configuration
{
    public static class QueueServiceCollectionExtension
    {
        public static IServiceCollection AddQueueTransport(this IServiceCollection services)
        {
            services.AddTransient<IQueueMiddlewareBuilder, QueueMiddlewareBuilder>();
            services.AddSingleton<QueueMiddleware>();
            services.AddHostedService<QueuePollingService>();
            services.AddTransient<QueueInputChannel>();
            return services;
        }
    }
}
