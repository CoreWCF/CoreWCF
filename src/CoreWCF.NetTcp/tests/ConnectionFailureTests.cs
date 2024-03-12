// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

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
        public async Task ServiceReceiveTimeoutAbortsChannelAsync()
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
                    var addressInUse = host.GetNetTcpAddressInUse();
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

        public class ReceiveTimeoutStartup
        {
            public const string BufferedRelatveAddress = "/nettcp.svc/Buffered";
            public const string StreamedRelatveAddress = "/nettcp.svc/Streamed";

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
