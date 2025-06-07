// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Web.Services.Description;
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
            webHostBuilder.AddNetTcpRequiredServices()
                          .ConfigureNetTcp(options =>
                          {
                              var uriBuilder = new UriBuilder("net.tcp", ipAddress.ToString(), port);
                              options.Listen(uriBuilder.Uri);
                          });
            return webHostBuilder;
        }

        private static IWebHostBuilder AddNetTcpRequiredServices(this IWebHostBuilder webHostBuilder)
        {
            webHostBuilder.ConfigureServices(services =>
            {
                services.AddNetFramingServices();
                services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, NetTcpHostedService>());
                services.TryAddSingleton<NetTcpFramingOptionsSetup>();
                services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<KestrelServerOptions>, NetTcpFramingOptionsSetup>(provider => provider.GetRequiredService<NetTcpFramingOptionsSetup>()));
                services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<NetTcpOptions>, NetTcpServerOptionsSetup>());
                services.TryAddSingleton<SocketTransportFactory>();
            });
            return webHostBuilder;
        }

        public static IWebHostBuilder UseNetTcp(this IWebHostBuilder webHostBuilder, Action<NetTcpOptions> options)
        {
            webHostBuilder.AddNetTcpRequiredServices()
                          .ConfigureNetTcp(options);
            return webHostBuilder;
        }

        public static IWebHostBuilder ConfigureNetTcp(this IWebHostBuilder webHostBuilder, Action<NetTcpOptions> options)
        {
            return webHostBuilder.ConfigureServices(services =>
            {
                services.Configure(options);
            });
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
        private readonly NetTcpOptions _options;
        private readonly IServiceBuilder _serviceBuilder;

        public NetTcpFramingOptionsSetup(IOptions<NetTcpOptions> options, IServiceBuilder serviceBuilder, ILogger<NetTcpFramingOptions> logger, ILogger<FramingConnection> framingConnectionLogger, IServiceProvider serviceProvider)
        {
            _options = options.Value ?? new NetTcpOptions();
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
            foreach(var tcpListenOptions in _options.CodeBackedListenOptions)
            {
                var baseAddress = tcpListenOptions.BaseAddress;
                IPAddress listenAddress = null;
                if (baseAddress.HostNameType == UriHostNameType.IPv4 || baseAddress.HostNameType == UriHostNameType.IPv6)
                {
                    if (IPAddress.TryParse(baseAddress.DnsSafeHost, out listenAddress))
                    {
                        if (listenAddress.AddressFamily == AddressFamily.InterNetwork && !Socket.OSSupportsIPv4)
                        {
                            _logger.LogError("NetTcp listen uri specified the IPv4 hostname \"{HostName}\" and the OS doesn't support IPv4", baseAddress.DnsSafeHost);
                            continue;
                        }
                        if (listenAddress.AddressFamily == AddressFamily.InterNetworkV6 && !Socket.OSSupportsIPv6)
                        {
                            _logger.LogError("NetTcp listen uri specified the IPv6 hostname \"{HostName}\" and the OS doesn't support IPv6", baseAddress.DnsSafeHost);
                            continue;
                        }
                    }
                }
                else if (baseAddress.HostNameType == UriHostNameType.Dns)
                {
                    if (Socket.OSSupportsIPv6)
                    {
                        listenAddress = IPAddress.IPv6Any;
                    }
                    else
                    {
                        listenAddress = IPAddress.Any;
                    }
                }
                else
                {
                    _logger.LogError("NetTcp listen uri specified an unusable hostname \"{HostName}\"", baseAddress.DnsSafeHost);
                    continue;
                }

                IPEndPoint endpoint = new IPEndPoint(listenAddress, baseAddress.Port);
                options.Listen(endpoint, builder =>
                {
                    AddTcpListenOptionsToConnectionContext(builder, tcpListenOptions);
                    builder.Use(ConvertExceptionsAndAddLogging);
                    builder.UseConnectionHandler<NetMessageFramingConnectionHandler>();
                    // Save the ListenOptions to be able to get final port number for adding BaseAddresses later
                    ListenOptions.Add(builder);
                    // Save the Kestrel ListenOptions in the TcpListenOptions to enable getting the final listening IPEndpoint
                    // This helps to get the port number being used if specified as 0, after the server has started.
                    tcpListenOptions.ListenOptions = builder;
                });
            }
            //foreach (IPEndPoint endpoint in _options.EndPoints)
            //{
            //    options.Listen(endpoint, builder =>
            //    {
            //        builder.Use(ConvertExceptionsAndAddLogging);
            //        builder.UseConnectionHandler<NetMessageFramingConnectionHandler>();
            //        // Save the ListenOptions to be able to get final port number for adding BaseAddresses later
            //        ListenOptions.Add(builder);
            //    });
            //}

            _serviceBuilder.Opening += OnServiceBuilderOpening;
        }

        private void AddTcpListenOptionsToConnectionContext(ListenOptions builder, TcpListenOptions tcpListenOptions)
        {
            builder.Use(next =>
            {
                tcpListenOptions.Use(innerNext =>
                {
                    return (ConnectionContext context) =>
                    {
                        context.Features.Set<NetFramingListenOptions>(tcpListenOptions);
                        return innerNext(context);
                    };
                });
                tcpListenOptions.Use(_ =>
                {
                    return (ConnectionContext context) =>
                    {
                        return next(context);
                    };
                });
                var tcpListenOptionsMiddleware = ((IConnectionBuilder)tcpListenOptions).Build();
                return tcpListenOptionsMiddleware;
            });
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
