// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Net;
using CoreWCF.Channels.Framing;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoreWCF.Configuration
{
    public static class ServiceModelWebHostBuilderExtensions
    {
        public static IWebHostBuilder UseNetTcp(this IWebHostBuilder webHostBuilder)
        {
            // Using default port for net.tcp
            return webHostBuilder.UseNetTcp(808);
        }

        public static IWebHostBuilder UseNetTcp(this IWebHostBuilder webHostBuilder, int port)
        {
            return webHostBuilder.UseNetTcp(IPAddress.Any, port);
        }

        public static IWebHostBuilder UseNetTcp(this IWebHostBuilder webHostBuilder, IPAddress ipAddress)
        {
            return webHostBuilder.UseNetTcp(ipAddress, 808);
        }

        public static IWebHostBuilder UseNetTcp(this IWebHostBuilder webHostBuilder, IPAddress ipAddress, int port)
        {
            webHostBuilder.ConfigureServices(services =>
            {
                services.TryAddEnumerable(ServiceDescriptor.Singleton<ITransportServiceBuilder, NetTcpTransportServiceBuilder>());
                services.AddSingleton(NetMessageFramingConnectionHandler.BuildAddressTable);
                services.AddNetTcpServices(new IPEndPoint(ipAddress, port));
                services.AddTransient<IFramingConnectionHandshakeBuilder, FramingConnectionHandshakeBuilder>();
            });

            return webHostBuilder;
        }

        private static IServiceCollection AddNetTcpServices(this IServiceCollection services, IPEndPoint endPoint)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<KestrelServerOptions>, NetTcpFramingOptionsSetup>());
            services.TryAddSingleton<NetMessageFramingConnectionHandler>();
            services.Configure<NetTcpFramingOptions>(o =>
            {
                o.EndPoints.Add(endPoint);
            });

            return services;
        }
    }

    // Exposes options for how to bind. This could include port sharing in the future
    internal class NetTcpFramingOptions
    {
        public List<IPEndPoint> EndPoints { get; } = new List<IPEndPoint>();
    }

    internal class NetTcpFramingOptionsSetup : IConfigureOptions<KestrelServerOptions>
    {
        private readonly ILogger<NetTcpFramingOptions> _logger;
        private readonly NetTcpFramingOptions _options;
        private readonly IServiceBuilder _serviceBuilder;

        public NetTcpFramingOptionsSetup(IOptions<NetTcpFramingOptions> options, IServiceBuilder serviceBuilder, ILogger<NetTcpFramingOptions> logger)
        {
            _options = options.Value ?? new NetTcpFramingOptions();
            _serviceBuilder = serviceBuilder;
            _logger = logger;
        }

        public List<ListenOptions> ListenOptions { get; } = new List<ListenOptions>();

        public void Configure(KestrelServerOptions options)
        {
            foreach (IPEndPoint endpoint in _options.EndPoints)
            {
                options.Listen(endpoint, builder =>
                {
                    builder.UseConnectionHandler<NetMessageFramingConnectionHandler>();
                    NetMessageFramingConnectionHandler handler = builder.ApplicationServices.GetRequiredService<NetMessageFramingConnectionHandler>();
                    // Save the ListenOptions to be able to get final port number for adding BaseAddresses later
                    ListenOptions.Add(builder);
                });
            }

            _serviceBuilder.Opening += OnServiceBuilderOpening;
        }

        private void OnServiceBuilderOpening(object sender, EventArgs e)
        {
            foreach (ListenOptions listenOptions in ListenOptions)
            {
                IPEndPoint endpoint = listenOptions.IPEndPoint;
                var baseAddress = new Uri($"net.tcp://{endpoint.Address}:{endpoint.Port}/");
                _logger.LogDebug($"Adding base address {baseAddress} to ServiceBuilderOptions");
                _serviceBuilder.BaseAddresses.Add(baseAddress);
            }
        }
    }
}
