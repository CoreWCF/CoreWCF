// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Channels;
using CoreWCF.Queue;
using Microsoft.Extensions.DependencyInjection;

namespace CoreWCF.Configuration
{
    public static class ServiceCollectionExtension
    {
        public static IServiceCollection AddServiceModelRabbitMqSupport(this IServiceCollection services)
        {
            services.AddSingleton<IQueueTransportFactory, RabbitMqTransportFactory>();
            services.AddSingleton(RabbitMqConnectionHandler.BuildAddressTable);
            services.AddSingleton<IQueueConnectionHandler, RabbitMqConnectionHandler>();

            return services;
        }
    }
}
