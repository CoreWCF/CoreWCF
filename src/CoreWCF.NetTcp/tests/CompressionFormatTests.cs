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
    public class CompressionFormatTests
    {
        private readonly ITestOutputHelper _output;

        public CompressionFormatTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [MemberData(nameof(GetTestVariations))]
        public void CompressionFormatTest(
            MessageVersion messageVersion,
            System.ServiceModel.Channels.MessageVersion clientMessageVersion,
            CompressionFormat compressionFormat,
            System.ServiceModel.Channels.CompressionFormat clientCompressionFormat)
        {
            string testString = new string('a', 10000);
            var webHostBuilder = ServiceHelper.CreateWebHostBuilder<Tests.Startup>(_output);
            webHostBuilder.ConfigureServices(services => services.AddServiceModelServices());
            webHostBuilder.Configure(app =>
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.TestService>();
                    builder.AddServiceEndpoint<Services.TestService, ServiceContract.ITestService>(GetServerBinding(messageVersion, compressionFormat), "/MessageVersionTest.svc");
                });
            });
            var host = webHostBuilder.Build();
            using (host)
            {
                host.Start();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(GetClientBinding(clientMessageVersion, clientCompressionFormat),
                    new System.ServiceModel.EndpointAddress(host.GetNetTcpAddressInUse() + "/MessageVersionTest.svc"));
                ClientContract.ITestService channel = factory.CreateChannel();
                var result = channel.EchoString(testString);
                Assert.Equal(testString, result);
                ((System.ServiceModel.IClientChannel)channel).Close();
            }
        }

        public static IEnumerable<object[]> GetTestVariations()
        {
            yield return new object[]
            {
                MessageVersion.Soap12WSAddressing10,
                System.ServiceModel.Channels.MessageVersion.CreateVersion(System.ServiceModel.EnvelopeVersion.Soap12, System.ServiceModel.Channels.AddressingVersion.WSAddressing10),
                CompressionFormat.GZip,
                System.ServiceModel.Channels.CompressionFormat.GZip
            };
            yield return new object[] {
                MessageVersion.Soap12WSAddressingAugust2004,
                System.ServiceModel.Channels.MessageVersion.Soap12WSAddressingAugust2004,
                CompressionFormat.GZip,
                System.ServiceModel.Channels.CompressionFormat.GZip
            };
            yield return new object[]
            {
                MessageVersion.Soap12WSAddressing10,
                System.ServiceModel.Channels.MessageVersion.CreateVersion(System.ServiceModel.EnvelopeVersion.Soap12, System.ServiceModel.Channels.AddressingVersion.WSAddressing10),
                CompressionFormat.Deflate,
                System.ServiceModel.Channels.CompressionFormat.Deflate
            };
            yield return new object[] {
                MessageVersion.Soap12WSAddressingAugust2004,
                System.ServiceModel.Channels.MessageVersion.Soap12WSAddressingAugust2004,
                CompressionFormat.Deflate,
                System.ServiceModel.Channels.CompressionFormat.Deflate
            };
        }

        internal class Startup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app) { }
        }

        internal static Binding GetServerBinding(MessageVersion messageVersion, CompressionFormat compressionFormat)
        {
            var netTcpBinding = new NetTcpBinding();
            netTcpBinding.Security.Mode = SecurityMode.None;

            var customBinding = new CustomBinding(netTcpBinding);
            var binaryEncoding = customBinding.Elements.Find<BinaryMessageEncodingBindingElement>();
            binaryEncoding.MessageVersion = messageVersion;
            binaryEncoding.CompressionFormat = compressionFormat;

            return customBinding;
        }

        internal static System.ServiceModel.Channels.Binding GetClientBinding(
            System.ServiceModel.Channels.MessageVersion clientMessageVersion,
            System.ServiceModel.Channels.CompressionFormat compressionFormat)
        {
            var netTcpBinding = new System.ServiceModel.NetTcpBinding();
            netTcpBinding.Security.Mode = System.ServiceModel.SecurityMode.None;

            var customBinding = new System.ServiceModel.Channels.CustomBinding(netTcpBinding);
            var binaryEncoding = customBinding.Elements.Find<System.ServiceModel.Channels.BinaryMessageEncodingBindingElement>();
            binaryEncoding.MessageVersion = clientMessageVersion;
            binaryEncoding.CompressionFormat = compressionFormat;

            return customBinding;
        }
    }
}
