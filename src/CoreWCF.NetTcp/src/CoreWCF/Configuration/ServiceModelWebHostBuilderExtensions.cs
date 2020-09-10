using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CoreWCF.Channels.Framing;
using System;
using System.Collections.Generic;
using System.Net;

namespace CoreWCF.Configuration
{
    public static class ServiceModelWebHostBuilderExtensions
    {
        public static IWebHostBuilder UseNetTcp(this IWebHostBuilder webHostBuilder)
        {
            return webHostBuilder.UseNetTcp(808);
        }

        public static IWebHostBuilder UseNetTcp(this IWebHostBuilder webHostBuilder, int port)
        {
            // Using default port
            webHostBuilder.ConfigureServices(services =>
            {
                services.TryAddEnumerable(ServiceDescriptor.Singleton<ITransportServiceBuilder, NetTcpTransportServiceBuilder>());
                services.AddSingleton(NetMessageFramingConnectionHandler.BuildAddressTable);
                services.AddNetTcpServices(new IPEndPoint(IPAddress.Any, port));
                services.AddTransient<IFramingConnectionHandshakeBuilder, FramingConnectionHandshakeBuilder>();
            });

            return webHostBuilder;
        }

        private static IServiceCollection AddNetTcpServices(this IServiceCollection services, IPEndPoint endPoint)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IConfigureOptions<KestrelServerOptions>, NetTcpFramingOptionsSetup>());

            services.Configure<NetTcpFramingOptions>(o =>
            {
                o.EndPoints.Add(endPoint);
            });

            //
            // In cases where we're only binding to net.tcp, we still need this feature,
            // in order to support automatically assigning TCP ports at runtime
            //
            services.TryAddSingleton<IFeatureCollection>(r =>
            {
                var features = new FeatureCollection();
                features.Set<IServerAddressesFeature>(new ServerAddressesFeature());
                return features;
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

        public NetTcpFramingOptionsSetup(IOptions<NetTcpFramingOptions> options, ILogger<NetTcpFramingOptions> logger)
        {
            _logger = logger;
            _options = options.Value;
        }

        public void Configure(KestrelServerOptions options)
        {
            IServiceBuilder serviceBuilder = options.ApplicationServices.GetRequiredService<IServiceBuilder>();
            foreach (var endpoint in _options.EndPoints)
            {
                options.Listen(endpoint, builder =>
                {
                    builder.UseConnectionHandler<NetMessageFramingConnectionHandler>();
                });

                var baseAddress = new Uri($"net.tcp://localhost:{endpoint.Port}/");

                if (endpoint.Port == 0)
                {
                    // Implicit port: it hasn't been assigned by the TCP stack yet
                    var serverFeatures = options.ApplicationServices.GetRequiredService<IFeatureCollection>();
                    var serverAddressesFeature = serverFeatures.Get<IServerAddressesFeature>();
                    serverAddressesFeature.Addresses.Add(baseAddress.ToString());
                }
                else
                {
                    // Explicit port: can safely add directly to the serviceBuilder (default behavior)
                    _logger.LogDebug($"Adding base address {baseAddress} to serviceBuilder");
                    serviceBuilder.BaseAddresses.Add(baseAddress);
                }
            }
        }
    }
}
