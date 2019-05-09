using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.ServiceModel.Channels.Framing;
using System;
using System.Collections.Generic;
using System.Net;

namespace Microsoft.ServiceModel.Configuration
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
        private readonly NetTcpFramingOptions _options;

        public NetTcpFramingOptionsSetup(IOptions<NetTcpFramingOptions> options)
        {
            _options = options.Value;
        }

        public void Configure(KestrelServerOptions options)
        {
            IServiceBuilder serviceBuilder = options.ApplicationServices.GetRequiredService<IServiceBuilder>();
            foreach (var endpoint in _options.EndPoints)
            {
                serviceBuilder.BaseAddresses.Add(new Uri($"net.tcp://localhost:{endpoint.Port}/"));
                options.Listen(endpoint, builder =>
                {
                    builder.UseConnectionHandler<NetMessageFramingConnectionHandler>();
                });
            }
        }
    }
}
