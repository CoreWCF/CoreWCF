// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;
#if NET472
using System.Security.Authentication;
#endif // NET472

namespace Helpers
{
    internal static class ServiceHelper
    {
        private const int HttpListenPort = 8080;
        private const int HttpsListenPort = 8443;

        internal static IWebHostBuilder CreateHttpWebHostBuilderWithMetadata<TService, TContract>(Binding binding, string url, ITestOutputHelper outputHelper = default) where TService : class
        {
            var customBinding = new CustomBinding(binding);
            ApplyDebugTimeouts(customBinding);
            var transportScheme = binding.Scheme;
            return CreateWebHostBuilder(outputHelper, transportScheme)
                .InlineStartup((IServiceCollection services) =>
                {
                    services.AddServiceModelServices()
                            .AddServiceModelMetadata();
                },
                app =>
                {
                    app.UseServiceModel(serviceBuilder =>
                    {
                        serviceBuilder.AddService<TService>();
                        serviceBuilder.AddServiceEndpoint<TService, TContract>(customBinding, url);
                    });
                    var serviceMetadataBehavior = app.ApplicationServices.GetRequiredService<CoreWCF.Description.ServiceMetadataBehavior>();
                    serviceMetadataBehavior.HttpGetEnabled = true;
                    serviceMetadataBehavior.HttpsGetEnabled = true;
                });
        }

        internal static string GetEndpointBaseAddress(IWebHost host, Binding binding)
        {
            string transportScheme = binding.Scheme;
            ICollection<string> addresses = host.ServerFeatures.Get<IServerAddressesFeature>().Addresses;
            foreach(string address in addresses)
            {
                var addressInUse = new Uri(address, UriKind.Absolute);
                var port = addressInUse.Port;
                if ("net.tcp".Equals(transportScheme, StringComparison.OrdinalIgnoreCase))
                {
                    if (port == HttpListenPort || port == HttpsListenPort)
                    {
                        continue;
                    }

                    return $"net.tcp://{addressInUse.Host}:{addressInUse.Port}";
                }
                else if("http".Equals(transportScheme, StringComparison.OrdinalIgnoreCase) && port == HttpListenPort)
                {
                    return $"http://{addressInUse.Host}:{addressInUse.Port}";
                }
                else if ("https".Equals(transportScheme, StringComparison.OrdinalIgnoreCase)&& port == HttpsListenPort)
                {
                    return $"https://{addressInUse.Host}:{addressInUse.Port}";
                }
            }

            throw new InvalidOperationException($"There are no listening addresses for scheme {transportScheme}. Available addresses are {string.Join(", ", addresses)}");
        }

        private static IWebHostBuilder CreateWebHostBuilder(ITestOutputHelper outputHelper, string transportScheme)
        {
            var host = WebHost.CreateDefaultBuilder(Array.Empty<string>());
#if DEBUG
            host.ConfigureLogging((ILoggingBuilder logging) =>
            {
                if (outputHelper != default)
                    logging.AddProvider(new XunitLoggerProvider(outputHelper));
                logging.AddFilter("Default", LogLevel.Debug);
                logging.AddFilter("Microsoft", LogLevel.Debug);
                logging.SetMinimumLevel(LogLevel.Debug);
            });
#endif // DEBUG
            host.UseKestrel(options =>
            {
                options.AllowSynchronousIO = true;
                {
                    // Always listen on http
                    options.ListenLocalhost(HttpListenPort, listenOptions =>
                    {
                        if (Debugger.IsAttached)
                        {
                            listenOptions.UseConnectionLogging();
                        }
                    });
                    // Optionally listen on https port
                    bool useHttps = "https".Equals(transportScheme, StringComparison.OrdinalIgnoreCase);
                    if (useHttps)
                    {
                        options.ListenLocalhost(HttpsListenPort, listenOptions =>
                        {
                            if (useHttps)
                            {
                                listenOptions.UseHttps(httpsOptions =>
                                {
#if NET472
                            httpsOptions.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls;
#endif // NET472
                            });
                            }

                            if (Debugger.IsAttached)
                            {
                                listenOptions.UseConnectionLogging();
                            }
                        });
                    }
                }
            });

            if("net.tcp".Equals(transportScheme, StringComparison.OrdinalIgnoreCase))
            {
                host.UseNetTcp(IPAddress.Loopback, 0);
            }

            return host;
        }

        private static readonly TimeSpan s_debugTimeout = TimeSpan.FromMinutes(20);

        private static void ApplyDebugTimeouts(Binding binding)
        {
            if (Debugger.IsAttached)
            {
                binding.OpenTimeout =
                    binding.CloseTimeout =
                    binding.SendTimeout =
                    binding.ReceiveTimeout = s_debugTimeout;
            }
        }

        private static IWebHostBuilder InlineStartup(this IWebHostBuilder webHostBuilder, Action<IServiceCollection> configureServices, Action<IApplicationBuilder> configure)
        {
            return webHostBuilder.ConfigureServices(services =>
            {
                var startupInstance = new StartupDelegateWrapper(configureServices, configure);
                services.AddSingleton(typeof(IStartup), startupInstance);
            });
        }

        private class StartupDelegateWrapper : StartupBase
        {
            private Action<IServiceCollection> _configureServices;
            private Action<IApplicationBuilder> _configure;

            public StartupDelegateWrapper(Action<IServiceCollection> configureServices, Action<IApplicationBuilder> configure)
            {
                _configureServices = configureServices;
                _configure = configure;
            }

            public override void Configure(IApplicationBuilder app)
            {
                _configure?.Invoke(app);
            }

            public override void ConfigureServices(IServiceCollection services)
            {
                _configureServices?.Invoke(services);
                base.ConfigureServices(services);
            }
        }
    }
}
