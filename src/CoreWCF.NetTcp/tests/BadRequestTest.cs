// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.NetTcp.Tests
{
    public class BadRequestTest
    {
        private readonly ITestOutputHelper _output;

        public BadRequestTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void UsingWrongUrlShouldNotThrowNullReferenceException()
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output, IPAddress.Loopback).Build();

            using (host)
            {
                host.Start();
                var netTcpBinding = ClientHelper.GetBufferedModeBinding();
                var testServiceFactory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(netTcpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri($"{host.GetNetTcpAddressInUse()}/TestService.svc/1")));
                var testServiceChannel = testServiceFactory.CreateChannel();


                // Verify the EchoService throws exception because of invalid url
                string toEcho = "hello, world!";
                Assert.Throws<System.ServiceModel.EndpointNotFoundException>(() => testServiceChannel.EchoString(toEcho));
                host.AssertNoExceptionsLogged<NullReferenceException>();
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
                    builder.AddServiceEndpoint<Services.TestService, ServiceContract.ITestService>(new NetTcpBinding(SecurityMode.None), "/TestService.svc/0");
                });
            }
        }
    }
}
