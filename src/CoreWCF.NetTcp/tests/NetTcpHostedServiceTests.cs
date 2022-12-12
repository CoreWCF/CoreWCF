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
    public class NetTcpHostedServiceTests
    {
        private readonly ITestOutputHelper _output;

        public NetTcpHostedServiceTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        [Trait("Category", "WindowsOnly")]  // HttpSys not supported on Linux
#if NET5_0_OR_GREATER
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
#endif
        public void HostedServiceDuplexEcho()
        {
            // This test uses a non Kestrel IServer implementation (HttpSys) to trigger code path
            // which uses an IHostedService. In the future this test should switch to using
            // the .NET Generic Host (https://learn.microsoft.com/en-us/dotnet/core/extensions/generic-host)
            // It's not available on asp.net core 2.1 so we couldn't test on .NET Framework
            string testString = new string('a', 3000);
            IWebHost host = ServiceHelper.CreateNonKestrelWebHostBuilder<Startup>(_output).Build();
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

            public void Configure(IApplicationBuilder app)
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
