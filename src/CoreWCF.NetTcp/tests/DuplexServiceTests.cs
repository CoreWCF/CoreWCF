// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.ServiceModel.Channels;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.NetTcp.Tests
{
    public class DuplexServiceTests
    {
        private readonly ITestOutputHelper _output;

        public DuplexServiceTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void DuplexEcho()
        {
            string testString = new string('a', 3000);
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                System.ServiceModel.ChannelFactory<ServiceContract.IDuplexTestService> factory = null;
                ServiceContract.IDuplexTestService channel = null;
                host.Start();
                try
                {
                    System.ServiceModel.NetTcpBinding binding = ClientHelper.GetBufferedModeBinding();
                    var callback = new ServiceContract.DuplexTestCallback();
                    factory = new System.ServiceModel.DuplexChannelFactory<ServiceContract.IDuplexTestService>(
                        new System.ServiceModel.InstanceContext(callback),
                        binding,
                        new System.ServiceModel.EndpointAddress(host.GetNetTcpAddressInUse() + Startup.DuplexRelativeAddress));
                    channel = factory.CreateChannel();
                    ((IChannel)channel).Open();
                    var registerSuccess = channel.RegisterDuplexChannel();
                    Assert.True(registerSuccess, "Registration was not successful");
                    channel.SendMessage(testString);
                    Assert.Equal(1, callback.ReceivedMessages.Count);
                    Assert.Equal(testString, callback.ReceivedMessages[0]);
                    ((IChannel)channel).Close();
                    factory.Close();
                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((IChannel)channel, factory);
                }
            }
        }

        public class Startup
        {
            public const string DuplexRelativeAddress = "/nettcp.duplex.svc/";

            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app, IHostingEnvironment env)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.DuplexTestService>();
                    builder.AddServiceEndpoint<Services.DuplexTestService, ServiceContract.IDuplexTestService>(new CoreWCF.NetTcpBinding(SecurityMode.None), DuplexRelativeAddress);
                });
            }
        }
    }
}
