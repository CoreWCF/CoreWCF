// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests
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
        public void EchoStringTest(Channels.MessageVersion messageVersion, System.ServiceModel.Channels.MessageVersion clientMessageVersion)
        {
            string testString = new string('a', 10);
            var webHostBuilder = ServiceHelper.CreateWebHostBuilder<Startup>(_output);
            webHostBuilder.ConfigureServices(services => services.AddServiceModelServices());
            webHostBuilder.Configure(app =>
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.EchoService>();
                    builder.AddServiceEndpoint<Services.EchoService, ServiceContract.IEchoService>(Startup.GetServerBinding(messageVersion), "/MessageVersionTest.svc");
                });
            });
            var host = webHostBuilder.Build();
            using (host)
            {
                host.Start();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(Startup.GetClientBinding(clientMessageVersion),
                    new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/MessageVersionTest.svc")));
                ClientContract.IEchoService channel = factory.CreateChannel();
                var result = channel.EchoString(testString);
                Assert.Equal(testString, result);
                ((System.ServiceModel.IClientChannel)channel).Close();
            }
        }

        public static IEnumerable<object[]> GetTestVariations()
        {
            yield return new object[] { MessageVersion.Soap11, System.ServiceModel.Channels.MessageVersion.Soap11 };
            yield return new object[] { MessageVersion.Soap11WSAddressing10, System.ServiceModel.Channels.MessageVersion.CreateVersion(System.ServiceModel.EnvelopeVersion.Soap11, System.ServiceModel.Channels.AddressingVersion.WSAddressing10) };
            yield return new object[] { MessageVersion.Soap11WSAddressingAugust2004, System.ServiceModel.Channels.MessageVersion.Soap11WSAddressingAugust2004 };
            yield return new object[] { MessageVersion.Soap12, System.ServiceModel.Channels.MessageVersion.CreateVersion(System.ServiceModel.EnvelopeVersion.Soap12, System.ServiceModel.Channels.AddressingVersion.None) };
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
                    new TextMessageEncodingBindingElement { MessageVersion = messageVersion },
                    new HttpTransportBindingElement { AuthenticationScheme = System.Net.AuthenticationSchemes.Anonymous });
            }

            internal static System.ServiceModel.Channels.Binding GetClientBinding(System.ServiceModel.Channels.MessageVersion clientMessageVersion)
            {
                return new System.ServiceModel.Channels.CustomBinding(
                    new System.ServiceModel.Channels.TextMessageEncodingBindingElement { MessageVersion = clientMessageVersion },
                    new System.ServiceModel.Channels.HttpTransportBindingElement { AuthenticationScheme = System.Net.AuthenticationSchemes.Anonymous });
            }
        }
    }
}
