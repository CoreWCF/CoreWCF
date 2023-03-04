// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests
{
    public class ByRefTests
    {
        private readonly ITestOutputHelper _output;

        public ByRefTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task ByRefParams()
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                await host.StartAsync();

                System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();

                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IByRefService>(
                    httpBinding,
                    new System.ServiceModel.EndpointAddress(
                        new Uri("http://localhost:8080/BasicWcfService/IByRefService.svc")));

                ClientContract.IByRefService channel = factory.CreateChannel();

                // Test that out param behaves as expected

                channel.SetNumber(33);

                channel.GetNumber(out int num1);

                Assert.Equal(33, num1);

                // Test that InAttribute makes no difference

                channel.SetNumberIn(41);

                channel.GetNumber(out int num2);

                Assert.Equal(41, num2);

                // Test that the out param is correctly decided by the input

                channel.GetOutParam("test", out Guid guidA, true);
                channel.GetOutParam("test", out Guid guidB, false);

                Assert.Equal(Services.ByRefService.GuidA, guidA);
                Assert.Equal(Services.ByRefService.GuidB, guidB);

                // Test that the ref param is changed between ResultA and ResultB and the return value is correct

                Guid refGuid = Services.ByRefService.GuidA;

                bool exchangeResult1 = channel.ExchangeRefParam(ref refGuid);

                Assert.True(exchangeResult1);
                Assert.Equal(Services.ByRefService.GuidB, refGuid);

                bool exchangeResult2 = channel.ExchangeRefParam(ref refGuid);

                Assert.True(exchangeResult2);
                Assert.Equal(Services.ByRefService.GuidA, refGuid);

                Guid unknownGuid = Guid.Parse("11112222-3333-4444-5555-666677778888");

                refGuid = unknownGuid;

                bool exchangeResult3 = channel.ExchangeRefParam(ref refGuid);

                Assert.False(exchangeResult3);
                Assert.Equal(unknownGuid, refGuid);

                // Test with both out and ref params. Chose which value is set using the bool argument.

                string resultA = "unknown";
                channel.SelectParam("test1", true, ref resultA, out string resultB);

                Assert.Equal("test1", resultA);
                Assert.Null(resultB);

                string resultC = "test3";
                channel.SelectParam("test2", false, ref resultC, out string resultD);

                Assert.Equal("test2", resultD);
                Assert.Equal("test3", resultC);
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
                    builder.AddService<Services.ByRefService>();
                    builder.AddServiceEndpoint<Services.ByRefService, ServiceContract.IByRefService>(new BasicHttpBinding(), "/BasicWcfService/IByRefService.svc");
                });
            }
        }
    }
}
