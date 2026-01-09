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
    public class InjectedRemoteEndpointMessagePropertyTest
    {
        private readonly ITestOutputHelper _output;

        public InjectedRemoteEndpointMessagePropertyTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void SimpleTest()
        {
            IHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output, IPAddress.Loopback).Build();
            using (host)
            {
                host.Start();
                var netTcpBinding = ClientHelper.GetBufferedModeBinding();
                var testServiceFactory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(netTcpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri($"{host.GetNetTcpAddressInUse()}/TestService.svc")));
                var testServiceChannel = testServiceFactory.CreateChannel();
                string result = testServiceChannel.GetClientIpEndpointInjected();
                Assert.NotNull(result);
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
                });
            }
        }
    }
}
