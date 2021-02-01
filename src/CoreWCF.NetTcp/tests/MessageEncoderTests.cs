using System;
using System.ServiceModel.Channels;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ServiceContract;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.NetTcp.Tests
{
    public class MessageEncoderTests
    {
        private ITestOutputHelper _output;
        public MessageEncoderTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [InlineData("ClientCompressionDisabled")]
        [InlineData("ClientCompressionDisabledStreamed")]
        [InlineData("CompressionEnabledClientAndService")]
        [InlineData("CompressionEnabledClientAndServiceStreamed")]
        [InlineData("ServerCompressionDisabled")]
        public void BinaryMessageEncoderCompressionWithDiffTransferModesTest(string variation)
        {
            string testString = "hello";
            var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            CustomBinding nettcpBinding;
            System.ServiceModel.ChannelFactory<ClientContract.IBinaryMessageEncoderService> factory = null;

            using (host)
            {
                host.Start();
                switch(variation)
                {
                    //Enable Compression on server and disable compression on client with TransferMode Buffered
                    case "ClientCompressionDisabled":
                        nettcpBinding = ClientHelper.GetCustomClientBinding(CompressionFormat.None, System.ServiceModel.TransferMode.Buffered);
                        factory = new System.ServiceModel.ChannelFactory<ClientContract.IBinaryMessageEncoderService>(nettcpBinding, new System.ServiceModel.EndpointAddress(new Uri(host.GetNetTcpAddressInUse() + "/BasicWcfService/nettcp.svc/clientDisabledBuffered")));
                        break;
                    //Enable Compression on server and disable compression on client with TransferMode Streamed
                    case "ClientCompressionDisabledStreamed":
                        nettcpBinding = ClientHelper.GetCustomClientBinding(CompressionFormat.None, System.ServiceModel.TransferMode.StreamedResponse);
                        factory = new System.ServiceModel.ChannelFactory<ClientContract.IBinaryMessageEncoderService>(nettcpBinding, new System.ServiceModel.EndpointAddress(new Uri(host.GetNetTcpAddressInUse() + "/BasicWcfService/nettcp.svc/clientDisabledStreamed")));
                        break;
                    //Enable Compression on server and client with TransferMode Buffered
                    case "CompressionEnabledClientAndService":
                        nettcpBinding = ClientHelper.GetCustomClientBinding(CompressionFormat.Deflate, System.ServiceModel.TransferMode.Buffered);
                        factory = new System.ServiceModel.ChannelFactory<ClientContract.IBinaryMessageEncoderService>(nettcpBinding, new System.ServiceModel.EndpointAddress(new Uri(host.GetNetTcpAddressInUse() + "/BasicWcfService/nettcp.svc/bothEnabledBuffered")));
                        break;
                    //Enable Compression on server and client with TransferMode Streamed
                    case "CompressionEnabledClientAndServiceStreamed":
                        nettcpBinding = ClientHelper.GetCustomClientBinding(CompressionFormat.GZip, System.ServiceModel.TransferMode.Streamed);
                        factory = new System.ServiceModel.ChannelFactory<ClientContract.IBinaryMessageEncoderService>(nettcpBinding, new System.ServiceModel.EndpointAddress(new Uri(host.GetNetTcpAddressInUse() + "/BasicWcfService/nettcp.svc/bothEnabledStreamed")));
                        break;
                    //Enable Compression on Client and disable compression on Service with TransferMode Buffered
                    case "ServerCompressionDisabled":
                        nettcpBinding = ClientHelper.GetCustomClientBinding(CompressionFormat.GZip, System.ServiceModel.TransferMode.Buffered);
                        factory = new System.ServiceModel.ChannelFactory<ClientContract.IBinaryMessageEncoderService>(nettcpBinding, new System.ServiceModel.EndpointAddress(new Uri(host.GetNetTcpAddressInUse() + "/BasicWcfService/nettcp.svc/serviceDisabledBuffered")));
                        break;
                    default:
                        break;
                }
                
                var channel = factory.CreateChannel();
                if(variation == "ServerCompressionDisabled")
                {
                    Assert.Throws<System.ServiceModel.ProtocolException>(() => channel.EchoString(testString));
                }
                else
                {
                    Assert.Equal(testString, channel.EchoString(testString));
                    Assert.NotNull(channel.GetStream());
                }               
            }
        }

        internal class Startup
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
                    builder.AddServiceEndpoint<Services.BinaryMessageEncoderService, IBinaryMessageEncoderService>(ServiceHelper.GetCustomServerBinding(Channels.CompressionFormat.GZip, TransferMode.Buffered), "/BasicWcfService/nettcp.svc/clientDisabledBuffered");
                    builder.AddServiceEndpoint<Services.BinaryMessageEncoderService, IBinaryMessageEncoderService>(ServiceHelper.GetCustomServerBinding(Channels.CompressionFormat.Deflate, TransferMode.StreamedResponse), "/BasicWcfService/nettcp.svc/clientDisabledStreamed");
                    builder.AddServiceEndpoint<Services.BinaryMessageEncoderService, IBinaryMessageEncoderService>(ServiceHelper.GetCustomServerBinding(Channels.CompressionFormat.Deflate, TransferMode.Buffered), "/BasicWcfService/nettcp.svc/bothEnabledBuffered");
                    builder.AddServiceEndpoint<Services.BinaryMessageEncoderService, IBinaryMessageEncoderService>(ServiceHelper.GetCustomServerBinding(Channels.CompressionFormat.GZip, TransferMode.Streamed), "/BasicWcfService/nettcp.svc/bothEnabledStreamed");
                    builder.AddServiceEndpoint<Services.BinaryMessageEncoderService, IBinaryMessageEncoderService>(ServiceHelper.GetCustomServerBinding(Channels.CompressionFormat.None, TransferMode.Buffered), "/BasicWcfService/nettcp.svc/serviceDisabledBuffered");
                });
            }
        }
    }
}
