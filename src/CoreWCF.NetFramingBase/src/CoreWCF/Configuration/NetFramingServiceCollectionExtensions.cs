// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Channels.Framing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CoreWCF.Configuration
{
    public static class NetFramingServiceCollectionExtensions
    {
        public static IServiceCollection AddNetFramingServices(this IServiceCollection services)
        {
            services.TryAddSingleton<NetMessageFramingConnectionHandler>();
            services.TryAddSingleton(NetMessageFramingConnectionHandler.BuildAddressTable);
            services.TryAddTransient<IFramingConnectionHandshakeBuilder, FramingConnectionHandshakeBuilder>();
            return services;
        }
    }
}
