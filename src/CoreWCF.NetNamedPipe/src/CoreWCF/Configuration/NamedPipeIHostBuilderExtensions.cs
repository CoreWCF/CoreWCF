// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.Versioning;
using CoreWCF.Channels;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CoreWCF.Configuration
{
    [SupportedOSPlatform("windows")]
    public static class NamedPipeIHostBuilderExtensions
    {
        public static IHostBuilder UseNetNamedPipe(this IHostBuilder hostBuilder)
        {
            return hostBuilder.ConfigureServices((context, services) =>
            {
                services.AddNetFramingServices();
                services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, NetNamedPipeHostedService>());
                services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<NetNamedPipeOptions>, NetNamedPipeServerOptionsSetup>());
            });
        }

        public static IHostBuilder UseNetNamedPipe(this IHostBuilder hostBuilder, Action<NetNamedPipeOptions> options)
        {
            hostBuilder.UseNetNamedPipe().ConfigureNetNamedPipe(options);
            return hostBuilder;
        }

        public static IHostBuilder ConfigureNetNamedPipe(this IHostBuilder hostBuilder, Action<NetNamedPipeOptions> options)
        {
            return hostBuilder.ConfigureServices((context, services) =>
            {
                services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<NetNamedPipeOptions>, NetNamedPipeServerOptionsSetup>());
                services.Configure(options);
            });
        }
    }
}
