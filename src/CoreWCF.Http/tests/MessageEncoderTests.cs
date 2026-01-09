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
    public class MessageEncoderTests
    {
        private readonly ITestOutputHelper _output;

        public MessageEncoderTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [MemberData(nameof(GetTestVariations))]
        public void BinaryMessageEncoderCompressionFormat_EchoString(Type startupType, System.ServiceModel.Channels.Binding clientBinding)
        {
            string testString = new string('a', 3000);
            IHost host = ServiceHelper.CreateWebHostBuilder(_output, startupType).Build();
            using (host)
            {
                host.Start();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(clientBinding,
                    new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/BasicWcfService/basichttp.svc")));
                ClientContract.IEchoService channel = factory.CreateChannel();
                string result = channel.EchoString(testString);
                Assert.Equal(testString, result);
            }
        }

        public static IEnumerable<object[]> GetTestVariations()
        {
            yield return new object[] { typeof(BinaryEncoderWithGzipStartup), BinaryEncoderWithGzipStartup.GetClientBinding() };
            yield return new object[] { typeof(BinaryEncoderWithDeflateStartup), BinaryEncoderWithDeflateStartup.GetClientBinding() };
            yield return new object[] { typeof(BinaryEncoderNoCompressionStartup), BinaryEncoderNoCompressionStartup.GetClientBinding() };
        }

        internal class BinaryEncoderWithGzipStartup : Startup
        {
            protected override CompressionFormat CompressionFormat => CompressionFormat.GZip;
            public static System.ServiceModel.Channels.Binding GetClientBinding() => GetClientBinding(System.ServiceModel.Channels.CompressionFormat.GZip);
        }

        internal class BinaryEncoderWithDeflateStartup : Startup
        {
            protected override CompressionFormat CompressionFormat => CompressionFormat.Deflate;
            public static System.ServiceModel.Channels.Binding GetClientBinding() => GetClientBinding(System.ServiceModel.Channels.CompressionFormat.Deflate);
        }

        internal class BinaryEncoderNoCompressionStartup : Startup
        {
            protected override CompressionFormat CompressionFormat => CompressionFormat.None;
            public static System.ServiceModel.Channels.Binding GetClientBinding() => GetClientBinding(System.ServiceModel.Channels.CompressionFormat.None);
        }

        internal abstract class Startup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }
            public void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.EchoService>();
                    var binding = new CustomBinding();
                    binding.Elements.Add(new BinaryMessageEncodingBindingElement { CompressionFormat = CompressionFormat });
                    binding.Elements.Add(new HttpTransportBindingElement { MaxReceivedMessageSize = 200065536 });
                    builder.AddServiceEndpoint<Services.EchoService, ServiceContract.IEchoService>(binding, "/BasicWcfService/basichttp.svc");
                });
            }

            protected static System.ServiceModel.Channels.Binding GetClientBinding(System.ServiceModel.Channels.CompressionFormat compressionFormat)
            {
                var binding = new System.ServiceModel.Channels.CustomBinding();
                binding.Elements.Add(new System.ServiceModel.Channels.BinaryMessageEncodingBindingElement { CompressionFormat = compressionFormat });
                binding.Elements.Add(new System.ServiceModel.Channels.HttpTransportBindingElement { MaxReceivedMessageSize = 200065536 });
                return binding;
            }

            protected abstract CompressionFormat CompressionFormat { get; }
        }
    }
}
