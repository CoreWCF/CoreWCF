// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using CoreWCF.Channels;
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
            // NetNamedPipeHostedService.StartAsync needs to trigger building the address table, but the
            // UriPrefixTable type that exists in NetFramingBase is internal, so we also register it as
            // its implemented interface type as an alias to be able to resolve it.
            services.TryAddSingleton<IEnumerable<KeyValuePair<BaseUriWithWildcard, HandshakeDelegate>>>(services =>
                services.GetRequiredService<UriPrefixTable<HandshakeDelegate>>());
            services.TryAddTransient<IFramingConnectionHandshakeBuilder, FramingConnectionHandshakeBuilder>();
            return services;
        }
    }
}
