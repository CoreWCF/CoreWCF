// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.NetTcp.Tests
{
    public class NetTcpServiceDispatcherTests
    {
        private readonly ITestOutputHelper _output;

        public NetTcpServiceDispatcherTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task ValidateListenUriPassedFromTransport()
        {
            string testString = new string('a', 3000);
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                await host.StartAsync();
                var dispatcherBuilder = host.Services.GetRequiredService<IDispatcherBuilder>();
                var dispatcher = dispatcherBuilder.BuildDispatchers(typeof(Services.TestService))[0];
                var binding = new CustomBinding(dispatcher.Binding);
                var lavbe = binding.Elements.Find<ListenAddressVerifyingBindingElement>();
                Assert.NotNull(lavbe.ListenUriBaseAddress);
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
                    var binding = AddTestBindingElement(new NetTcpBinding(SecurityMode.None));
                    builder.AddServiceEndpoint<Services.TestService, ServiceContract.ITestService>(binding, "/TestService.svc");
                });
            }

            private static Binding AddTestBindingElement(Binding binding)
            {
                var customBinding = new CustomBinding(binding);
                customBinding.Elements.Insert(1, new ListenAddressVerifyingBindingElement());
                return customBinding;
            }
        }
    }
}
