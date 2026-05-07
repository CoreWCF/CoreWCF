// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
using CoreWCF;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

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

        [Fact]
        public void WebSocket_Http_VerifyWebSocketsUsed()
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<StartupCreateNotificationOnConnection>(_output).Build();
            using (host)
            {
                System.ServiceModel.ChannelFactory<ClientContract.IVerifyWebSockets> factory = null;
                ClientContract.IVerifyWebSockets channel = null;
                host.Start();
                try
                {
                    System.ServiceModel.NetHttpBinding binding = ClientHelper.GetBufferedModeWebSocketBinding();
                    factory = new System.ServiceModel.ChannelFactory<ClientContract.IVerifyWebSockets>(binding,
                        new System.ServiceModel.EndpointAddress(new Uri(GetNetHttpBufferedServiceUri(host))));
                    channel = factory.CreateChannel();
                    ((IChannel)channel).Open();
                    bool responseFromService = channel.ValidateWebSocketsUsed();
                    Assert.True(responseFromService, String.Format("Response from the service was not expected. Expected: 'True' but got {0}", responseFromService));
                    ((IChannel)channel).Close();
                    factory.Close();
                }
                finally
                {
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

        public class StartupCreateNotificationOnConnection
        {
            public const string BufferedPath = "/nethttp.svc/buffered";

            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.VerifyWebSockets>();
                    var binding = new NetHttpBinding();
                    binding.WebSocketSettings.TransportUsage = CoreWCF.Channels.WebSocketTransportUsage.Always;
                    binding.WebSocketSettings.CreateNotificationOnConnection = true;
                    builder.AddServiceEndpoint<Services.VerifyWebSockets, ServiceContract.IVerifyWebSockets>(binding, BufferedPath);
                });
            }
        }

        [Theory]
        [InlineData(System.ServiceModel.NetHttpMessageEncoding.Text)]
        [InlineData(System.ServiceModel.NetHttpMessageEncoding.Binary)]
        public void WebSocket_Http_RequestReply_Streamed(System.ServiceModel.NetHttpMessageEncoding messageEncoding)
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<StartupRequestReplyStreamed>(_output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.ChannelFactory<ClientContract.IRequestReplyService> factory = null;
                ClientContract.IRequestReplyService client = null;
                try
                {
                    var binding = new System.ServiceModel.NetHttpBinding
                    {
                        MaxReceivedMessageSize = 67108864,
                        MaxBufferSize = 67108864,
                        TransferMode = System.ServiceModel.TransferMode.Streamed,
                        MessageEncoding = messageEncoding
                    };
                    binding.WebSocketSettings.TransportUsage = WebSocketTransportUsage.Always;

                    string encodingName = messageEncoding.ToString();
                    string address = $"{GetNetHttpServiceBaseUri(host)}{StartupRequestReplyStreamed.StreamedPath}{encodingName}";
                    factory = new System.ServiceModel.ChannelFactory<ClientContract.IRequestReplyService>(
                        binding, new System.ServiceModel.EndpointAddress(new Uri(address)));
                    client = factory.CreateChannel();
                    ((IChannel)client).Open();

                    // Download a stream from the service
                    using (Stream stream = client.DownloadStream())
                    {
                        int readResult;
                        byte[] buffer = new byte[1000];
                        do
                        {
                            readResult = stream.Read(buffer, 0, buffer.Length);
                        }
                        while (readResult != 0);
                    }

                    // Upload a stream to the service
                    var uploadStream = new FlowControlledStream
                    {
                        ReadThrottle = TimeSpan.FromMilliseconds(500),
                        StreamDuration = TimeSpan.FromSeconds(1)
                    };
                    client.UploadStream(uploadStream);

                    // Validate via log
                    List<string> log = client.GetLog();
                    Assert.True(log.Count > 0, "The server log should contain entries after stream operations.");

                    // Close - this is the critical call from dotnet/wcf#5818
                    ((IChannel)client).Close();
                    factory.Close();
                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((IChannel)client, factory);
                }
            }
        }

        public class StartupRequestReplyStreamed
        {
            public const string StreamedPath = "/nethttp.svc/reqreply-streamed/";

            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.RequestReplyService>();

                    // Register one endpoint per encoding so client and server agree on message format
                    foreach (CoreWCF.NetHttpMessageEncoding encoding in Enum.GetValues(typeof(CoreWCF.NetHttpMessageEncoding)))
                    {
                        var binding = new CoreWCF.NetHttpBinding
                        {
                            TransferMode = CoreWCF.TransferMode.Streamed,
                            MaxReceivedMessageSize = 67108864,
                            MaxBufferSize = 67108864,
                            MessageEncoding = encoding,
                        };
                        binding.WebSocketSettings.TransportUsage = CoreWCF.Channels.WebSocketTransportUsage.Always;
                        builder.AddServiceEndpoint<Services.RequestReplyService, ServiceContract.IRequestReplyService>(
                            binding, StreamedPath + encoding.ToString());
                    }
                });
            }
        }

        private record ServiceEndpoint(CoreWCF.Channels.Binding Binding, string Path);
    }
}
