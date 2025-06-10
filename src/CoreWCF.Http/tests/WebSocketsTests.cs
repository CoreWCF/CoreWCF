// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
using CoreWCF;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace NetHttp
{
    public class WebSocketsTests
    {
        private const string NetHttpServiceBaseUriFormat = "http://localhost:{0}";
        private static string GetNetHttpServiceBaseUri(IWebHost webHost)
            => string.Format(NetHttpServiceBaseUriFormat, webHost.GetHttpPort());
        private static string GetNetHttpBufferedServiceUri(IWebHost webHost)
            => string.Concat(GetNetHttpServiceBaseUri(webHost), Startup.BufferedPath);
        private static string GetNetHttpDuplexServiceUri(IWebHost webHost)
            => string.Concat(GetNetHttpServiceBaseUri(webHost), StartupUsingDuplexService.DuplexPath);

        private readonly ITestOutputHelper _output;

        public WebSocketsTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void NetHttpWebSocketsSettingsApplied()
        {
            var netHttpBinding = new NetHttpBinding();
            var webSocketSettings = netHttpBinding.WebSocketSettings;
            ModifyWebSocketSettings(webSocketSettings);
            var htbe = netHttpBinding.CreateBindingElements().Find<CoreWCF.Channels.HttpTransportBindingElement>();
            Assert.Equal(webSocketSettings, htbe.WebSocketSettings);
            netHttpBinding = new NetHttpBinding(CoreWCF.Channels.BasicHttpSecurityMode.Transport);
            webSocketSettings = netHttpBinding.WebSocketSettings;
            ModifyWebSocketSettings(webSocketSettings);
            var htsbe = netHttpBinding.CreateBindingElements().Find<CoreWCF.Channels.HttpsTransportBindingElement>();
            Assert.Equal(webSocketSettings, htsbe.WebSocketSettings);

            void ModifyWebSocketSettings(CoreWCF.Channels.WebSocketTransportSettings settings)
            {
                settings.SubProtocol = "foo";
                settings.KeepAliveInterval = TimeSpan.FromMinutes(100);
                settings.TransportUsage = CoreWCF.Channels.WebSocketTransportUsage.Always;
                settings.CreateNotificationOnConnection = true;
                settings.DisablePayloadMasking = true;
                settings.MaxPendingConnections = 12345;
            }
        }

        [Fact]
        public async Task NetHttpWebSocketsWorkWithNullSubProtocol()
        {
            var serverBinding = new CoreWCF.Channels.CustomBinding()
            {
                Elements =
                {
                    new CoreWCF.Channels.BinaryMessageEncodingBindingElement(),
                    new CoreWCF.Channels.HttpTransportBindingElement
                    {
                        WebSocketSettings = { SubProtocol = null, TransportUsage = CoreWCF.Channels.WebSocketTransportUsage.Always }
                    }
                }
            };
            var path = "/websocket";
            using var host = ServiceHelper.CreateWebHostBuilder<StartupWithInjectedServicepoint>(_output)
                .ConfigureServices(services =>
                {
                    services.AddSingleton(new ServiceEndpoint(serverBinding, path));
                })
                .Build();

            await host.StartAsync();

            System.ServiceModel.Channels.Binding clientBinding = new CustomBinding()
            {
                Elements =
                {
                    new BinaryMessageEncodingBindingElement(),
                    new HttpTransportBindingElement
                    {
                        WebSocketSettings = { SubProtocol = null, TransportUsage = WebSocketTransportUsage.Always}
                    }
                }
            };

            using var channelFactory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(
                clientBinding,
                new System.ServiceModel.EndpointAddress(new Uri(GetNetHttpServiceBaseUri(host) + path)));
            var client = channelFactory.CreateChannel();

            try
            {
                client.EchoString("Hello world");
            }
            finally
            {
                if (client is IDisposable clientChannel)
                    clientChannel.Dispose();
            }
        }

        [Fact]
        public void NetHttpWebSocketsBufferedTransferMode()
        {
            string testString = new string('a', 3000);
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                System.ServiceModel.ChannelFactory<ClientContract.IEchoService> factory = null;
                ClientContract.IEchoService channel = null;
                host.Start();
                try
                {
                    System.ServiceModel.NetHttpBinding binding = ClientHelper.GetBufferedModeWebSocketBinding();
                    factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(binding,
                        new System.ServiceModel.EndpointAddress(new Uri(GetNetHttpBufferedServiceUri(host))));
                    channel = factory.CreateChannel();
                    ((IChannel)channel).Open();
                    string result = channel.EchoString(testString);
                    Assert.Equal(testString, result);
                    ((IChannel)channel).Close();
                    factory.Close();
                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((IChannel)channel, factory);
                }
            }
        }

        [Fact]
        public void WebSocketEndpointReturnBadRequestForHttpRequest()
        {
            string testString = new string('a', 3000);
            IWebHost host = ServiceHelper.CreateWebHostBuilder<StartupUsingDuplexService>(_output).Build();
            using (host)
            {
                System.ServiceModel.ChannelFactory<ClientContract.IEchoService> factory = null;
                ClientContract.IEchoService channel = null;
                host.Start();
                try
                {
                    System.ServiceModel.BasicHttpBinding binding = new System.ServiceModel.BasicHttpBinding();
                    factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(binding,
                        new System.ServiceModel.EndpointAddress(new Uri(GetNetHttpDuplexServiceUri(host))));
                    channel = factory.CreateChannel();
                    ((IChannel)channel).Open();
                    var exception = Assert.Throws<System.ServiceModel.ProtocolException>(() => channel.EchoString(testString));
                    Assert.Contains("This service only supports WebSocket connections.", exception.Message);
                }
                finally
                {
                    ((IChannel)channel).Close();
                    factory.Close();
                    ServiceHelper.CloseServiceModelObjects((IChannel)channel, factory);
                }
            }
        }

        //[Fact]
        //public void NetHttpWebSocketsStreamedTransferMode()
        //{
        //    string testString = new string('a', 3000);
        //    var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
        //    using (host)
        //    {
        //        System.ServiceModel.ChannelFactory<ClientContract.IEchoService> factory = null;
        //        ClientContract.IEchoService channel = null;
        //        host.Start();
        //        try
        //        {
        //            var binding = ClientHelper.GetStreamedModeWebSocketBinding();
        //            factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(binding,
        //                new System.ServiceModel.EndpointAddress(new Uri(NetHttpServiceUri)));
        //            channel = factory.CreateChannel();
        //            ((IChannel)channel).Open();
        //            var result = channel.EchoString(testString);
        //            Assert.Equal(testString, result);
        //            ((IChannel)channel).Close();
        //            factory.Close();
        //        }
        //        finally
        //        {
        //            ServiceHelper.CloseServiceModelObjects((IChannel)channel, factory);
        //        }
        //    }
        //}


        public class Startup
        {
            public const string BufferedPath = "/nethttp.svc/buffered";
            public const string StreamedPath = "/nethttp.svc/streamed";

            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.EchoService>();
                    var binding = new NetHttpBinding();
                    binding.WebSocketSettings.TransportUsage = CoreWCF.Channels.WebSocketTransportUsage.Always;
                    builder.AddServiceEndpoint<Services.EchoService, ServiceContract.IEchoService>(binding, BufferedPath);
                });
            }
        }

        private class StartupWithInjectedServicepoint
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app)
            {
                var (binding, path) = app.ApplicationServices.GetRequiredService<ServiceEndpoint>();
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.EchoService>();
                    builder.AddServiceEndpoint<Services.EchoService, ServiceContract.IEchoService>(binding, path);
                });
            }
        }

        public class StartupUsingDuplexService
        {
            public const string DuplexPath = "/nethttp.svc/duplex";

            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.DuplexTestService>();
                    var binding = new NetHttpBinding();
                    binding.WebSocketSettings.TransportUsage = CoreWCF.Channels.WebSocketTransportUsage.Always;
                    builder.AddServiceEndpoint<Services.DuplexTestService, ServiceContract.IDuplexTestService>(binding, DuplexPath);
                });
            }
        }

        private record ServiceEndpoint(CoreWCF.Channels.Binding Binding, string Path);
    }
}
