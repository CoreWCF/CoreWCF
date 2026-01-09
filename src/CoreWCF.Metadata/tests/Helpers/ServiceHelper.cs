// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.Security;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;
using Xunit;
using CoreWCF;

#if NET472
using System.Security.Authentication;
#endif // NET472

namespace Helpers
{
    internal static class ServiceHelper
    {
        private const int HttpListenPort = 8080;
        private const int HttpsListenPort = 8443;

        internal static IWebHostBuilder CreateHttpWebHostBuilderWithMetadata<TService, TContract>(Binding binding, string url,
            Action<IServiceCollection> configureServices = null,
            ITestOutputHelper outputHelper = default,
            Action<ServiceHostBase> configureHost = null,
            string callerMethodName = "")
            where TService : class
        {

            var customBinding = new CustomBinding(binding);
            ApplyDebugTimeouts(customBinding);
            var transportScheme = binding.Scheme;
            return CreateWebHostBuilder<TService>(outputHelper, new[] { transportScheme }, callerMethodName)
                .InlineStartup((IServiceCollection services) =>
                {
                    services.AddServiceModelServices()
                            .AddServiceModelMetadata();
                    configureServices?.Invoke(services);
                },
                app =>
                {
                    var portHelper = app.ApplicationServices.GetRequiredService<ListeningPortHelper>();
                    app.UseServiceModel(serviceBuilder =>
                    {
                        serviceBuilder.AddService<TService>(options =>
                        {
                            options.BaseAddresses.Clear();
                            foreach(var scheme in portHelper.Schemes)
                            {
                                if (scheme != Uri.UriSchemeNetPipe)
                                {
                                    options.BaseAddresses.Add(new Uri($"{scheme}://localhost:{portHelper.GetPortForScheme(scheme)}"));
                                }
                                else
                                {
                                    // NetPipe doesn't use a port, so just add the pipe name
                                    options.BaseAddresses.Add(new Uri($"{scheme}://localhost/{typeof(TService).Name}/"));
                                }
                            }
                        });
                        serviceBuilder.AddServiceEndpoint<TService, TContract>(customBinding, url);
                        if (configureHost != null)
                        {
                            serviceBuilder.ConfigureServiceHostBase<TService>(configureHost);
                        }
                    });
                    var serviceMetadataBehavior = app.ApplicationServices.GetRequiredService<CoreWCF.Description.ServiceMetadataBehavior>();
                    if (transportScheme == Uri.UriSchemeHttp)
                    {
                        serviceMetadataBehavior.HttpGetEnabled = true;
                    }
                    else if (transportScheme == Uri.UriSchemeHttps)
                    {
                        serviceMetadataBehavior.HttpsGetEnabled = true;
                    }
                    else
                    {
                        serviceMetadataBehavior.HttpGetEnabled = true;
                        // If we aren't testing an HTTP based binding, then need to explicitly set the WSDL url using the path
                        serviceMetadataBehavior.HttpGetUrl = new Uri($"http://localhost:{HttpListenPort}{url}");
                    }
                });
        }

        internal static IWebHostBuilder CreateHttpWebHostBuilderWithMetadata<TService1, TService2, TContract1, TContract2>(Binding binding1, Binding binding2, string url, ITestOutputHelper outputHelper = default, string callerMethodName = "") where TService1 : class where TService2 : class
        {
            var customBinding1 = new CustomBinding(binding1);
            ApplyDebugTimeouts(customBinding1);
            var transportScheme1 = binding1.Scheme;
            var customBinding2 = new CustomBinding(binding2);
            ApplyDebugTimeouts(customBinding2);
            var transportScheme2 = binding1.Scheme;
            return CreateWebHostBuilder<TService1>(outputHelper, new[] { transportScheme1, transportScheme2 }, callerMethodName)
                .InlineStartup((IServiceCollection services) =>
                {
                    services.AddServiceModelServices()
                            .AddServiceModelMetadata();
                },
                app =>
                {
                    app.UseServiceModel(serviceBuilder =>
                    {
                        var portHelper = app.ApplicationServices.GetRequiredService<ListeningPortHelper>();
                        serviceBuilder.AddService<TService1>(serviceOptions =>
                        {
                            var baseAddressBuilder = new UriBuilder($"http://localhost/1{url}");
                            baseAddressBuilder.Scheme = customBinding1.Scheme;
                            var port = portHelper.GetPortForScheme(customBinding1.Scheme);
                            if (port == 0)
                            {
                                Assert.Fail($"No port found for scheme {customBinding1.Scheme}, available port mappings are: {portHelper}");
                            }
                            baseAddressBuilder.Port = port;
                            serviceOptions.BaseAddresses.Add(baseAddressBuilder.Uri);
                            if (!customBinding1.Scheme.StartsWith(Uri.UriSchemeHttp))
                            {
                                // Binding isn't HTTP or HTTPS so need to add a second base address to support the metadata endpoint
                                port = portHelper.GetPortForScheme(Uri.UriSchemeHttp);
                                if (port == 0)
                                {
                                    Assert.Fail($"No port found for scheme {Uri.UriSchemeHttp}, available port mappings are: {portHelper}");
                                }
                                serviceOptions.BaseAddresses.Add(new Uri($"http://localhost:{port}/1{url}"));
                            }
                        });
                        serviceBuilder.AddServiceEndpoint<TService1, TContract1>(customBinding1, customBinding1.Scheme);
                        serviceBuilder.AddService<TService2>(serviceOptions =>
                        {
                            var baseAddressBuilder = new UriBuilder($"http://localhost:/2{url}");
                            baseAddressBuilder.Scheme = customBinding2.Scheme;
                            var port = portHelper.GetPortForScheme(customBinding2.Scheme);
                            if (port == 0)
                            {
                                Assert.Fail($"No port found for scheme {customBinding2.Scheme}, available port mappings are: {portHelper}");
                            }
                            baseAddressBuilder.Port = port;
                            serviceOptions.BaseAddresses.Add(baseAddressBuilder.Uri);
                            if (!customBinding2.Scheme.StartsWith(Uri.UriSchemeHttp))
                            {
                                // Binding isn't HTTP or HTTPS so need to add a second base address to support the metadata endpoint
                                port = portHelper.GetPortForScheme(Uri.UriSchemeHttp);
                                if (port == 0)
                                {
                                    Assert.Fail($"No port found for scheme {Uri.UriSchemeHttp}, available port mappings are: {portHelper}");
                                }
                                serviceOptions.BaseAddresses.Add(new Uri($"http://localhost:{port}/2{url}"));
                            }
                        });
                        serviceBuilder.AddServiceEndpoint<TService2, TContract2>(customBinding2, customBinding2.Scheme);
                    });
                    var serviceMetadataBehavior = app.ApplicationServices.GetRequiredService<CoreWCF.Description.ServiceMetadataBehavior>();
                    serviceMetadataBehavior.HttpGetEnabled = true;
                });
        }

        internal static IWebHostBuilder CreateHttpWebHostBuilderWithMetadata<TService, TContract>(IDictionary<string, Binding> bindingEndpointMap, Uri[] baseAddresses, ITestOutputHelper outputHelper = default, string callerMethodName = "") where TService : class
        {
            // Presume all bindings are the same type
            var transportSchemes = bindingEndpointMap.Values.Select(binding => binding.Scheme).ToArray();
            return CreateWebHostBuilder<TService>(outputHelper, transportSchemes, callerMethodName)
                .InlineStartup((IServiceCollection services) =>
                {
                    services.AddServiceModelServices()
                            .AddServiceModelMetadata();
                },
                app =>
                {
                    app.UseServiceModel(serviceBuilder =>
                    {
                        var portHelper = app.ApplicationServices.GetRequiredService<ListeningPortHelper>();
                        serviceBuilder.AddService<TService>(serviceOptions =>
                        {
                            foreach (var address in baseAddresses)
                            {
                                var baseAddressBuilder = new UriBuilder(address);
                                var port = portHelper.GetPortForScheme(baseAddressBuilder.Scheme);
                                if (port == 0)
                                {
                                    Assert.Fail($"No port found for scheme {baseAddressBuilder.Scheme}, available port mappings are: {portHelper}");
                                }

                                baseAddressBuilder.Port = port;
                                serviceOptions.BaseAddresses.Add(baseAddressBuilder.Uri);
                            }
                        });
                        foreach (var bindingEndpoint in bindingEndpointMap)
                        {
                            var customBinding = new CustomBinding(bindingEndpoint.Value);
                            ApplyDebugTimeouts(customBinding);
                            serviceBuilder.AddServiceEndpoint<TService, TContract>(customBinding, bindingEndpoint.Key);
                        }
                        serviceBuilder.ConfigureServiceHostBase<TService>(host =>
                        {
                            var serviceCreds = host.Credentials;
                            serviceCreds.UserNameAuthentication.UserNamePasswordValidationMode = UserNamePasswordValidationMode.Custom;
                            serviceCreds.UserNameAuthentication.CustomUserNamePasswordValidator = new AcceptAnyUserNamePasswordValidator();
                        });
                    });
                    var serviceMetadataBehavior = app.ApplicationServices.GetRequiredService<CoreWCF.Description.ServiceMetadataBehavior>();
                    var baseAddressSchemes = baseAddresses.Select(a => a.Scheme).ToArray();
                    if (baseAddressSchemes.Contains(Uri.UriSchemeHttp))
                    {
                        serviceMetadataBehavior.HttpGetEnabled = true;
                    }

                    if (baseAddressSchemes.Contains(Uri.UriSchemeHttps))
                    {
                        serviceMetadataBehavior.HttpsGetEnabled = true;
                    }
                });
        }

        internal static string GetEndpointBaseAddress(IHost host, string transportScheme)
        {

            if (transportScheme == Uri.UriSchemeNetPipe)
            {
                var serviceBuilder = host.Services.GetRequiredService<IServiceBuilder>();
                return serviceBuilder.BaseAddresses.Single(a => a.Scheme == Uri.UriSchemeNetPipe).ToString();
            }

            var portHelper = host.Services.GetRequiredService<ListeningPortHelper>();
            IEnumerable<Uri> addresses = host.ServerFeatures.Get<IServerAddressesFeature>().Addresses.Select(a => new Uri(a));
            var listeningPort = portHelper.GetPortForScheme(transportScheme);
            if (listeningPort == 0)
            {
                throw new InvalidOperationException($"No calculable listening port found for scheme {transportScheme}, known ports are {portHelper}");
            }

            var serverAddress = addresses.Single(a => a.Port == listeningPort);
            var baseAddressUriBuilder = new UriBuilder(serverAddress);
            baseAddressUriBuilder.Scheme = transportScheme;
            if (baseAddressUriBuilder.Host == "127.0.0.1")
            {
                baseAddressUriBuilder.Host = "localhost";
            }

            return baseAddressUriBuilder.Uri.ToString();
        }

        private static IWebHostBuilder CreateWebHostBuilder<TService>(ITestOutputHelper outputHelper, IEnumerable<string> transportSchemes, string callerMethodName) where TService : class
        {
            var schemesCollection = new HashSet<string>(transportSchemes, StringComparer.OrdinalIgnoreCase);
            var host = WebHost.CreateDefaultBuilder(Array.Empty<string>());
            var listeningPortHelper = new ListeningPortHelper();
            host.ConfigureServices(services =>
            {
                services.AddSingleton(listeningPortHelper);
            });
#if DEBUG
            host.ConfigureLogging((ILoggingBuilder logging) =>
            {
                if (outputHelper != default)
                    logging.AddProvider(new XunitLoggerProvider(outputHelper, callerMethodName));
                logging.AddFilter("Default", LogLevel.Debug);
                logging.AddFilter("Microsoft", LogLevel.Debug);
                logging.SetMinimumLevel(LogLevel.Debug);
            });
#endif // DEBUG
            host.UseKestrel(options =>
            {
                // Always listen on http
                options.Listen(IPAddress.Loopback, 0, listenOptions =>
                {
                    if (Debugger.IsAttached)
                    {
                        listenOptions.UseConnectionLogging();
                    }
                    listeningPortHelper.AddSchemeToPortDelegate(Uri.UriSchemeHttp, () => listenOptions?.IPEndPoint?.Port ?? 0 );
                });
                // Optionally listen on https port
                bool useHttps = schemesCollection.Contains(Uri.UriSchemeHttps);
                if (useHttps)
                {
                    options.Listen(IPAddress.Loopback, 0, listenOptions =>
                    {
                        listenOptions.UseHttps(httpsOptions =>
                        {
#if NET472
                            httpsOptions.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls;
#endif // NET472
                        });

                        listeningPortHelper.AddSchemeToPortDelegate(Uri.UriSchemeHttps, () => listenOptions?.IPEndPoint?.Port ?? 0);

                        if (Debugger.IsAttached)
                        {
                            listenOptions.UseConnectionLogging();
                        }
                    });
                }
            });

            if (schemesCollection.Contains(Uri.UriSchemeNetTcp))
            {
                host.UseNetTcp(netTcpOptions =>
                {
                    netTcpOptions.Listen("net.tcp://127.0.0.1:0/", tcpOptions =>
                    {
                        listeningPortHelper.AddSchemeToPortDelegate(Uri.UriSchemeNetTcp, () => tcpOptions?.IPEndpoint?.Port ?? 0);
                    });
                });
            }

#if NET8_0_OR_GREATER
            if (OperatingSystem.IsWindows())
            {
#endif // NET8_0_OR_GREATER
                if (schemesCollection.Contains(Uri.UriSchemeNetPipe))
                {
                    host.UseNetNamedPipe(netPipeOptions =>
                    {
                        netPipeOptions.Listen($"net.pipe://localhost/{typeof(TService).Name}/", pipeOptions =>
                        {
                            listeningPortHelper.AddSchemeToPortDelegate(Uri.UriSchemeNetPipe, () => 0);
                        });
                    });
                }
#if NET8_0_OR_GREATER
            }
#endif // NET8_0_OR_GREATER

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

    internal class AcceptAnyUserNamePasswordValidator : UserNamePasswordValidator
    {
        public override ValueTask ValidateAsync(string userName, string password)
        {
            return new ValueTask(Task.CompletedTask);
        }
    }
}
