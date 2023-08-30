// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
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
    public class MessageVersionsTests
    {
        private readonly ITestOutputHelper _output;

        public MessageVersionsTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [MemberData(nameof(GetTestVariations))]
        public void EchoStringTest(MessageVersion messageVersion, System.ServiceModel.Channels.MessageVersion clientMessageVersion)
        {
            string testString = new string('a', 10);
            var webHostBuilder = ServiceHelper.CreateWebHostBuilder<Startup>(_output);
            webHostBuilder.ConfigureServices(services => services.AddServiceModelServices());
            webHostBuilder.Configure(app =>
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.TestService>();
                    builder.AddServiceEndpoint<Services.TestService, ServiceContract.ITestService>(Startup.GetServerBinding(messageVersion), "/MessageVersionTest.svc");
                });
            });
            var host = webHostBuilder.Build();
            using (host)
            {
                host.Start();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(Startup.GetClientBinding(clientMessageVersion),
                    new System.ServiceModel.EndpointAddress(host.GetNetTcpAddressInUse() + "/MessageVersionTest.svc"));
                ClientContract.ITestService channel = factory.CreateChannel();
                var result = channel.EchoString(testString);
                Assert.Equal(testString, result);
                ((System.ServiceModel.IClientChannel)channel).Close();
            }
        }

        public static IEnumerable<object[]> GetTestVariations()
        {
            yield return new object[] { MessageVersion.Soap12WSAddressing10, System.ServiceModel.Channels.MessageVersion.CreateVersion(System.ServiceModel.EnvelopeVersion.Soap12, System.ServiceModel.Channels.AddressingVersion.WSAddressing10) };
            yield return new object[] { MessageVersion.Soap12WSAddressingAugust2004, System.ServiceModel.Channels.MessageVersion.Soap12WSAddressingAugust2004 };
        }

        internal class Startup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app) { }

            internal static Binding GetServerBinding(MessageVersion messageVersion)
            {
                return new CustomBinding(
                    new BinaryMessageEncodingBindingElement { MessageVersion = messageVersion },
                    new TcpTransportBindingElement());
            }

            internal static System.ServiceModel.Channels.Binding GetClientBinding(System.ServiceModel.Channels.MessageVersion clientMessageVersion)
            {
                return new System.ServiceModel.Channels.CustomBinding(
                    new System.ServiceModel.Channels.BinaryMessageEncodingBindingElement { MessageVersion = clientMessageVersion },
                    new System.ServiceModel.Channels.TcpTransportBindingElement());
            }
        }
    }
}
