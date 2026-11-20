// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using CoreWCF.Configuration;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CoreWCF.Extensions.Configuration.Tests
{
    /// <summary>
    /// A CoreWCF host whose services and endpoints come entirely from configuration. Nothing here names a binding,
    /// a contract or an address - that is the point of the end to end tests.
    /// </summary>
    public class ConfiguredStartup
    {
        private readonly IConfiguration _configuration;

        public ConfiguredStartup(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddServiceModelServices();
            services.AddServiceModelConfiguration(_configuration.GetSection("ServiceModel"));
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseServiceModel();
        }
    }

    public static class ConfiguredServiceHost
    {
        /// <summary>
        /// Builds a Kestrel host listening on an arbitrary free port, configured from <paramref name="settings"/>.
        /// </summary>
        public static IWebHost CreateHttpHost(Dictionary<string, string> settings) =>
            CreateBuilder(settings)
                .UseKestrel(options => options.Listen(IPAddress.Loopback, 0))
                .Build();

        /// <summary>
        /// Builds a host with the net.tcp transport listening on <paramref name="netTcpPort"/>.
        /// </summary>
        public static IWebHost CreateNetTcpHost(Dictionary<string, string> settings, int netTcpPort) =>
            CreateBuilder(settings)
                .UseKestrel(options => options.Listen(IPAddress.Loopback, 0))
                .UseNetTcp(IPAddress.Loopback, netTcpPort)
                .Build();

        /// <summary>
        /// Reserves a free TCP port. net.tcp needs its port before configuration is built, unlike Kestrel which can
        /// be handed port 0 and asked afterwards.
        /// </summary>
        public static int GetAvailableTcpPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }

        private static IWebHostBuilder CreateBuilder(Dictionary<string, string> settings) =>
            WebHost.CreateDefaultBuilder(new string[0])
                .ConfigureAppConfiguration(builder =>
                {
                    builder.Sources.Clear();
                    builder.AddInMemoryCollection(settings);
                })
                .UseStartup<ConfiguredStartup>();
    }
}
