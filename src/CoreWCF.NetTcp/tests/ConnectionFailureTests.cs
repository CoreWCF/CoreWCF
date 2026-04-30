// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Net;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using System.Threading;

namespace CoreWCF.NetTcp.Tests
{
    public class ConnectionFailureTests
    {
        private readonly ITestOutputHelper _output;

        public ConnectionFailureTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task ServiceReceiveTimeoutAbortsChannel()
        {
            string testString = new string('a', 3000);
            IWebHost host = ServiceHelper.CreateWebHostBuilder<ReceiveTimeoutStartup>(_output).Build();
            using (host)
            {
                System.ServiceModel.ChannelFactory<ClientContract.ITestService> factory = null;
                ClientContract.ITestService channel = null;
                host.Start();
                try
                {
                    System.ServiceModel.NetTcpBinding binding = ClientHelper.GetBufferedModeBinding();
                    factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(binding,
                        new System.ServiceModel.EndpointAddress(host.GetNetTcpAddressInUse() + ReceiveTimeoutStartup.BufferedRelatveAddress));
                    channel = factory.CreateChannel();
                    ((IChannel)channel).Open();
                    string result = channel.EchoString(testString);
                    // Channel should be aborted by the server 10 seconds after it sent the last response
                    await Task.Delay(TimeSpan.FromSeconds(10) + TimeSpan.FromMilliseconds(500));
                    // Using a Stopwatch to time how long it takes for the exception to happen to ensure not caused by client side timeout
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    var exception = Assert.ThrowsAny<System.ServiceModel.CommunicationException>(() => _ = channel.EchoString(testString));
                    stopwatch.Stop();
                    // Allow up to 5 seconds to account for CPU contended runs on low power machines like in DevOps
                    Assert.InRange(stopwatch.Elapsed, TimeSpan.Zero, TimeSpan.FromSeconds(5));
                    Assert.IsType<System.Net.Sockets.SocketException>(exception.InnerException);
                    Assert.Equal(System.ServiceModel.CommunicationState.Faulted, ((IChannel)channel).State);
                    ((IChannel)channel).Abort();
                    factory.Close();
                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((IChannel)channel, factory);
                }
            }
        }

        [Fact]
        public async Task ServiceChannelInitializationTimeoutTest()
        {
            string testString = new string('a', 3000);
            IWebHost host = ServiceHelper.CreateWebHostBuilderWithoutNetTcp<ReceiveTimeoutStartup>(_output)
                .UseNetTcp(options =>
                {
                    options.Listen("net.tcp://localhost:0/", listenOptions =>
                    {
                        listenOptions.ConnectionPoolSettings.ChannelInitializationTimeout = TimeSpan.FromSeconds(5);
                    });
                })
                .Build();
            using (host)
            {
                host.Start();
                var addressInUse = host.GetNetTcpAddressInUse();
                var serviceUri = new Uri(addressInUse);
                int port = serviceUri.Port;
                var ipEndPoint = new IPEndPoint(IPAddress.Loopback, port);
                using var client = new Socket(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                client.ReceiveTimeout = 10_000;
                await client.ConnectAsync(ipEndPoint);
                Stopwatch stopwatch = Stopwatch.StartNew();
                var socketException = Assert.Throws<SocketException>(() => _ = client.Receive(new byte[1]));
                stopwatch.Stop();
                Assert.InRange(stopwatch.Elapsed, TimeSpan.FromSeconds(1.5), TimeSpan.FromSeconds(10));
            }
        }

        // Verifies that a client which completes the 5-byte version+mode preamble and then
        // closes the send side does not leave the server looping in the via-decode loop of
        // DuplexFramingMiddleware / SingletonFramingMiddleware. The server must honor
        // ChannelInitializationTimeout (or detect the premature EOF) and tear down the
        // connection within a reasonable bound.
        [Theory]
        [InlineData((byte)0x02)] // FramingMode.Duplex    -> DuplexFramingMiddleware
        [InlineData((byte)0x01)] // FramingMode.Singleton -> SingletonFramingMiddleware
        public async Task ClosedConnectionAfterPreambleClosesPromptly(byte framingMode)
        {
            const int channelInitializationTimeoutSeconds = 5;
            const int clientReceiveTimeoutMs = 20_000;

            IWebHost host = ServiceHelper.CreateWebHostBuilderWithoutNetTcp<ReceiveTimeoutStartup>(_output)
                .UseNetTcp(options =>
                {
                    options.Listen("net.tcp://localhost:0/", listenOptions =>
                    {
                        listenOptions.ConnectionPoolSettings.ChannelInitializationTimeout =
                            TimeSpan.FromSeconds(channelInitializationTimeoutSeconds);
                    });
                })
                .Build();

            using (host)
            {
                host.Start();
                var serviceUri = new Uri(host.GetNetTcpAddressInUse());
                var ipEndPoint = new IPEndPoint(IPAddress.Loopback, serviceUri.Port);

                using var client = new Socket(ipEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                client.NoDelay = true;
                client.ReceiveTimeout = clientReceiveTimeoutMs;
                await client.ConnectAsync(ipEndPoint);

                // .NET Message Framing preamble (MS-NMF):
                //   00 = Version record type, 01 00 = major.minor, 01 = Mode record type, NN = mode value.
                // These 5 bytes satisfy ServerModeDecoder/FramingModeHandshakeMiddleware and route
                // the connection into Duplex/SingletonFramingMiddleware where the via-record
                // (which we deliberately never send) is read.
                byte[] preamble = { 0x00, 0x01, 0x00, 0x01, framingMode };
                int sent = client.Send(preamble);
                Assert.Equal(preamble.Length, sent);

                // Close the send side. The server's PipeReader will now return
                // { Buffer = empty, IsCompleted = true } on every ReadAsync.
                client.Shutdown(SocketShutdown.Send);

                Stopwatch stopwatch = Stopwatch.StartNew();
                int bytesRead;
                try
                {
                    // The server must close (Abort -> RST, or clean FIN) within a small multiple
                    // of ChannelInitializationTimeout. Otherwise we hit the client-side receive
                    // timeout (~20s) and the assertion below fails.
                    bytesRead = client.Receive(new byte[1]);
                }
                catch (SocketException)
                {
                    // Expected: server aborted the connection (TCP RST observed as SocketException).
                    bytesRead = 0;
                }
                stopwatch.Stop();

                Assert.Equal(0, bytesRead);
                Assert.InRange(
                    stopwatch.Elapsed,
                    TimeSpan.Zero,
                    TimeSpan.FromSeconds(channelInitializationTimeoutSeconds + 10));
            }
        }

        public class ReceiveTimeoutStartup
        {
            public const string BufferedRelatveAddress = "/nettcp.svc/Buffered";

            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.TestService>();
                    var binding = new CoreWCF.NetTcpBinding(CoreWCF.SecurityMode.None);
                    binding.ReceiveTimeout = TimeSpan.FromSeconds(10);
                    builder.AddServiceEndpoint<Services.TestService, ServiceContract.ITestService>(binding, BufferedRelatveAddress);
                });
            }
        }
    }
}
