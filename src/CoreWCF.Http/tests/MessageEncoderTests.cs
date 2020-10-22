using CoreWCF.Channels;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests
{
    public class MessageEncoderTests
    {
        private ITestOutputHelper _output;

        public MessageEncoderTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [MemberData(nameof(GetTestVariations))]
        public void BinaryMessageEncoderCompressionFormat_EchoString(Type startupType, System.ServiceModel.Channels.Binding clientBinding)
        {
            string testString = new string('a', 3000);
            var host = ServiceHelper.CreateWebHostBuilder(_output, startupType).Build();
            using (host)
            {
                host.Start();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(clientBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/basichttp.svc")));
                var channel = factory.CreateChannel();
                var result = channel.EchoString(testString);
                Assert.Equal(testString, result);
            }
        }

        [Theory]
        [InlineData("ClientCompressionDisabled")]
        [InlineData("ClientCompressionDisabledStreamed")]
        [InlineData("CompressionEnabledClientAndService")]
#if NET472
        [InlineData("CompressionEnabledClientAndServiceStreamed")]
#endif
        [InlineData("ServerCompressionDisabled")]
        public void BinaryMessageEncoderCompressionWithDiffTransferModes(string variation)
        {
            string testString = new string('a', 3000);
            var host = ServiceHelper.CreateWebHostBuilder<CompressionWithDifferentTransferModesStartup>(_output).Build();
            System.ServiceModel.Channels.CustomBinding httpBinding;
            System.ServiceModel.ChannelFactory<ClientContract.IBinaryMessageEncoderService> factory = null;

            using (host)
            {
                host.Start();
                switch (variation)
                {
                    //Enable Compression on server and disable compression on client with TransferMode Buffered
                    case "ClientCompressionDisabled":
                        httpBinding = ClientHelper.GetCustomClientBinding(System.ServiceModel.Channels.CompressionFormat.None, System.ServiceModel.TransferMode.Buffered);
                        factory = new System.ServiceModel.ChannelFactory<ClientContract.IBinaryMessageEncoderService>(httpBinding, new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/basichttp.svc/clientDisabledBuffered")));
                        break;
                    //Enable Compression on server and disable compression on client with TransferMode Streamed
                    case "ClientCompressionDisabledStreamed":
                        httpBinding = ClientHelper.GetCustomClientBinding(System.ServiceModel.Channels.CompressionFormat.None, System.ServiceModel.TransferMode.StreamedResponse);
                        factory = new System.ServiceModel.ChannelFactory<ClientContract.IBinaryMessageEncoderService>(httpBinding, new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/basichttp.svc/clientDisabledStreamed")));
                        break;
                    //Enable Compression on server and client with TransferMode Buffered
                    case "CompressionEnabledClientAndService":
                        httpBinding = ClientHelper.GetCustomClientBinding(System.ServiceModel.Channels.CompressionFormat.GZip, System.ServiceModel.TransferMode.Buffered);
                        factory = new System.ServiceModel.ChannelFactory<ClientContract.IBinaryMessageEncoderService>(httpBinding, new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/basichttp.svc/bothEnabledBuffered")));
                        break;
                    //Enable Compression on server and client with TransferMode Streamed
                    case "CompressionEnabledClientAndServiceStreamed":
                        httpBinding = ClientHelper.GetCustomClientBinding(System.ServiceModel.Channels.CompressionFormat.Deflate, System.ServiceModel.TransferMode.Streamed);
                        factory = new System.ServiceModel.ChannelFactory<ClientContract.IBinaryMessageEncoderService>(httpBinding, new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/basichttp.svc/bothEnabledStreamed")));
                        break;
                    //Enable Compression on Client and disable compression on Service with TransferMode Buffered
                    case "ServerCompressionDisabled":
                        httpBinding = ClientHelper.GetCustomClientBinding(System.ServiceModel.Channels.CompressionFormat.GZip, System.ServiceModel.TransferMode.Buffered);
                        factory = new System.ServiceModel.ChannelFactory<ClientContract.IBinaryMessageEncoderService>(httpBinding, new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/basichttp.svc/serviceDisabledBuffered")));
                        break;
                    default:
                        break;
                }

                var channel = factory.CreateChannel();
                if (variation == "ServerCompressionDisabled")
                {
                    Assert.Throws<System.ServiceModel.CommunicationException>(() => channel.EchoString(testString));
                }
                else
                {
                    Assert.Equal(testString, channel.EchoString(testString));
                }
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
            public void Configure(IApplicationBuilder app, IHostingEnvironment env)
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

        internal class CompressionWithDifferentTransferModesStartup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app, IHostingEnvironment env)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.BinaryMessageEncoderService>();
                    builder.AddServiceEndpoint<Services.BinaryMessageEncoderService, ServiceContract.IBinaryMessageEncoderService>(ServiceHelper.GetCustomServerBinding(CompressionFormat.Deflate, TransferMode.Buffered), "/BasicWcfService/basichttp.svc/clientDisabledBuffered");
                    builder.AddServiceEndpoint<Services.BinaryMessageEncoderService, ServiceContract.IBinaryMessageEncoderService>(ServiceHelper.GetCustomServerBinding(CompressionFormat.Deflate, TransferMode.StreamedResponse), "/BasicWcfService/basichttp.svc/clientDisabledStreamed");
                    builder.AddServiceEndpoint<Services.BinaryMessageEncoderService, ServiceContract.IBinaryMessageEncoderService>(ServiceHelper.GetCustomServerBinding(CompressionFormat.GZip, TransferMode.Buffered), "/BasicWcfService/basichttp.svc/bothEnabledBuffered");
                    builder.AddServiceEndpoint<Services.BinaryMessageEncoderService, ServiceContract.IBinaryMessageEncoderService>(ServiceHelper.GetCustomServerBinding(CompressionFormat.Deflate, TransferMode.Streamed), "/BasicWcfService/basichttp.svc/bothEnabledStreamed");
                    builder.AddServiceEndpoint<Services.BinaryMessageEncoderService, ServiceContract.IBinaryMessageEncoderService>(ServiceHelper.GetCustomServerBinding(CompressionFormat.None, TransferMode.Buffered), "/BasicWcfService/basichttp.svc/serviceDisabledBuffered");
                });
            }
        }
    }
}
