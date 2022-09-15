// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net;
using System.Text;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.NetTcp.Tests
{
    public class MultipleServicesTest
    {
        private readonly ITestOutputHelper _output;

        public MultipleServicesTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void AddMultipleServices()
        {
            int port = 11808;
            string expectedBaseAddress = $"net.tcp://{IPAddress.Loopback}:{port}";
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output, IPAddress.Loopback, port).Build();
            using (host)
            {
                host.Start();
                Assert.Equal(expectedBaseAddress, host.GetNetTcpAddressInUse());

                var netTcpBinding = ClientHelper.GetBufferedModeBinding();
                var testServiceFactory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(netTcpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri($"{expectedBaseAddress}/TestService.svc")));
                var testServiceChannel = testServiceFactory.CreateChannel();

                var messageEncodingServiceFactory = new System.ServiceModel.ChannelFactory<ClientContract.IMessageEncodingService>(netTcpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri($"{expectedBaseAddress}/MessageEncodingService.svc")));
                var messageEncodingServiceChannel = messageEncodingServiceFactory.CreateChannel();

                // Verify the EchoService echoes the input string
                string toEcho = "hello, world!";
                string result = testServiceChannel.EchoString(toEcho);
                Assert.NotNull(result);
                Assert.Equal(toEcho, result);

                // Verify the MessageEncodingService echoes the input byte array
                byte[] bytesToEcho = Encoding.ASCII.GetBytes("hello, bytes!");
                byte[] bytesResult = messageEncodingServiceChannel.EchoByteArray(bytesToEcho);
                Assert.NotNull(result);
                Assert.Equal(bytesToEcho, bytesResult);
            }
        }

        internal class Startup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.TestService>();
                    builder.AddServiceEndpoint<Services.TestService, ServiceContract.ITestService>(new NetTcpBinding(SecurityMode.None), "/TestService.svc");

                    builder.AddService<Services.MessageEncodingService>();
                    builder.AddServiceEndpoint<Services.MessageEncodingService, ServiceContract.IMessageEncodingService>(new NetTcpBinding(SecurityMode.None), "/MessageEncodingService.svc");
                });
            }
        }
    }
}
