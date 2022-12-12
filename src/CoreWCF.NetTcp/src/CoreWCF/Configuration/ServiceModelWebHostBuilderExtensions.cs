// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Net;
using CoreWCF.Channels;
using CoreWCF.Channels.Framing;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
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
                services.AddNetFramingServices();
                services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, NetTcpHostedService>());
                services.TryAddSingleton<NetTcpFramingOptionsSetup>();
                services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<KestrelServerOptions>, NetTcpFramingOptionsSetup>(provider => provider.GetRequiredService<NetTcpFramingOptionsSetup>()));
                services.TryAddSingleton<SocketTransportFactory>();
                services.AddNetTcpServices(new IPEndPoint(ipAddress, port));
            });

            return webHostBuilder;
        }

        private static IServiceCollection AddNetTcpServices(this IServiceCollection services, IPEndPoint endPoint)
        {
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
        private readonly ILogger<FramingConnection> _framingConnectionLogger;
        private readonly IServiceProvider _serviceProvider;
        private readonly NetTcpFramingOptions _options;
        private readonly IServiceBuilder _serviceBuilder;

        public NetTcpFramingOptionsSetup(IOptions<NetTcpFramingOptions> options, IServiceBuilder serviceBuilder, ILogger<NetTcpFramingOptions> logger, ILogger<FramingConnection> framingConnectionLogger, IServiceProvider serviceProvider)
        {
            _options = options.Value ?? new NetTcpFramingOptions();
            _serviceBuilder = serviceBuilder;
            _logger = logger;
            _framingConnectionLogger = framingConnectionLogger;
            _serviceProvider = serviceProvider;
        }

        public List<ListenOptions> ListenOptions { get; } = new List<ListenOptions>();

        public bool ConfigureCalled { get; set; }

        public void Configure(KestrelServerOptions options)
        {
            ConfigureCalled = true;
            options.ApplicationServices = _serviceProvider;
            foreach (IPEndPoint endpoint in _options.EndPoints)
            {
                options.Listen(endpoint, builder =>
                {
                    builder.Use(ConvertExceptionsAndAddLogging);
                    builder.UseConnectionHandler<NetMessageFramingConnectionHandler>();
                    // Save the ListenOptions to be able to get final port number for adding BaseAddresses later
                    ListenOptions.Add(builder);
                });
            }

            _serviceBuilder.Opening += OnServiceBuilderOpening;
        }

        private ConnectionDelegate ConvertExceptionsAndAddLogging(ConnectionDelegate next)
        {
            return (ConnectionContext context) =>
            {
                var logger = new ConnectionIdWrappingLogger(_framingConnectionLogger, context.ConnectionId);
                context.Features.Set<ILogger>(logger);

                //TODO: Add a public api mechanism to enable connection logging in RELEASE build
#if DEBUG
                context.Transport = new NetTcpExceptionConvertingDuplexPipe(new LoggingDuplexPipe(context.Transport, logger) { LoggingEnabled = true });
#else
                context.Transport = new NetTcpExceptionConvertingDuplexPipe(context.Transport);
#endif
                return next(context);
            };
        }

        private void OnServiceBuilderOpening(object sender, EventArgs e)
        {
            UpdateServiceBuilderBaseAddresses();
        }

        internal void UpdateServiceBuilderBaseAddresses()
        {
            foreach (ListenOptions listenOptions in ListenOptions)
            {
                IPEndPoint endpoint = listenOptions.IPEndPoint;
                string address = endpoint.Address.ToString();
                if (endpoint.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                    address = $"[{address}]";
                var baseAddress = new Uri($"net.tcp://{address}:{endpoint.Port}/");
                _logger.LogDebug($"Adding base address {baseAddress} to ServiceBuilderOptions");
                _serviceBuilder.BaseAddresses.Add(baseAddress);
            }

            ListenOptions.Clear();
        }
    }
}
