// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Channels;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CoreWCF.Configuration
{
    public static class NamedPipeWebHostBuilderExtensions
    {
        public static IWebHostBuilder UseNetNamedPipe(this IWebHostBuilder webHostBuilder)
        {
            return webHostBuilder.ConfigureServices(services =>
            {
                services.AddNetFramingServices();
                services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, NetNamedPipeHostedService>());
                services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<NetNamedPipeOptions>, NetNamedPipeServerOptionsSetup>());
            });
        }

        public static IWebHostBuilder UseNetNamedPipe(this IWebHostBuilder webHostBuilder, Action<NetNamedPipeOptions> options)
        {
            webHostBuilder.UseNetNamedPipe().ConfigureNetNamedPipe(options);
            return webHostBuilder;
        }

        public static IWebHostBuilder ConfigureNetNamedPipe(this IWebHostBuilder webHostBuilder, Action<NetNamedPipeOptions> options)
        {
            return webHostBuilder.ConfigureServices(services =>
            {
                services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<NetNamedPipeOptions>, NetNamedPipeServerOptionsSetup>());
                services.Configure(options);
            });
        }
    }
}
