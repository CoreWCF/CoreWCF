// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Channels;
using Microsoft.Extensions.DependencyInjection;

namespace CoreWCF.Configuration
{
    public static class ServiceCollectionExtension
    {
        public static IServiceCollection AddServiceModelMsmqSupport(this IServiceCollection services)
        {
            services.AddSingleton<IDeadLetterQueueMsmqSender, DeadLetterQueueSender>();

            return services;
        }
    }
}
