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

namespace CoreWCF.Http.Tests
{
    public class BinaryMessageEncoderTests
    {
        private ITestOutputHelper _output;
        public BinaryMessageEncoderTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [InlineData("ClientCompressionDisabled")] //looks like known issue, ref: PR #143
        [InlineData("ClientCompressionDisabledStreamed")] //looks like known issue, ref: PR #143
        [InlineData("CompressionEnabledClientAndService")]
        [InlineData("CompressionEnabledClientAndServiceStreamed")]
        [InlineData("ServerCompressionDisabled")]
        public void Test(string variation)
        {
            string testString = new string('a', 3000);
            var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            CustomBinding httpBinding;
            System.ServiceModel.ChannelFactory<ClientContract.IBinaryMessageEncoderService> factory = null;

            using (host)
            {
                host.Start();
                switch(variation)
                {
                    //Enable Compression on server and disable compression on client with TransferMode Buffered
                    case "ClientCompressionDisabled":
                        httpBinding = ClientHelper.GetCustomClientBinding(CompressionFormat.None, CommonConstants.HttpTransport, System.ServiceModel.TransferMode.Buffered);
                        factory = new System.ServiceModel.ChannelFactory<ClientContract.IBinaryMessageEncoderService>(httpBinding, new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/basichttp.svc/clientDisabledBuffered")));
                        break;
                    //Enable Compression on server and disable compression on client with TransferMode Streamed
                    case "ClientCompressionDisabledStreamed":
                        httpBinding = ClientHelper.GetCustomClientBinding(CompressionFormat.None, CommonConstants.HttpTransport, System.ServiceModel.TransferMode.Streamed);
                        factory = new System.ServiceModel.ChannelFactory<ClientContract.IBinaryMessageEncoderService>(httpBinding, new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/basichttp.svc/clientDisabledStreamed")));
                        break;
                    //Enable Compression on server and client with TransferMode Buffered
                    case "CompressionEnabledClientAndService":
                        httpBinding = ClientHelper.GetCustomClientBinding(CompressionFormat.GZip, CommonConstants.HttpTransport, System.ServiceModel.TransferMode.Buffered);
                        factory = new System.ServiceModel.ChannelFactory<ClientContract.IBinaryMessageEncoderService>(httpBinding, new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/basichttp.svc/bothEnabledBuffered")));
                        break;
                    //Enable Compression on server and client with TransferMode Streamed
                    case "CompressionEnabledClientAndServiceStreamed":
                        httpBinding = ClientHelper.GetCustomClientBinding(CompressionFormat.Deflate, CommonConstants.HttpTransport, System.ServiceModel.TransferMode.Buffered);
                        factory = new System.ServiceModel.ChannelFactory<ClientContract.IBinaryMessageEncoderService>(httpBinding, new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/basichttp.svc/bothEnabledStreamed")));
                        break;
                    //Enable Compression on Client and disable compression on Service with TransferMode Buffered
                    case "ServerCompressionDisabled":
                        httpBinding = ClientHelper.GetCustomClientBinding(CompressionFormat.GZip, CommonConstants.HttpTransport, System.ServiceModel.TransferMode.Buffered);
                        factory = new System.ServiceModel.ChannelFactory<ClientContract.IBinaryMessageEncoderService>(httpBinding, new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/basichttp.svc/serviceDisabledBuffered")));
                        break;
                    default:
                        break;
                }
                
                var channel = factory.CreateChannel();
                Assert.Equal(testString, channel.EchoString(testString));
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
                    builder.AddServiceEndpoint<Services.BinaryMessageEncoderService, IBinaryMessageEncoderService>(ServiceHelper.GetCustomServerBinding(Channels.CompressionFormat.Deflate ,CommonConstants.HttpTransport, TransferMode.Buffered), "/BasicWcfService/basichttp.svc/clientDisabledBuffered");
                    builder.AddServiceEndpoint<Services.BinaryMessageEncoderService, IBinaryMessageEncoderService>(ServiceHelper.GetCustomServerBinding(Channels.CompressionFormat.Deflate, CommonConstants.HttpTransport, TransferMode.Streamed), "/BasicWcfService/basichttp.svc/clientDisabledStreamed");
                    builder.AddServiceEndpoint<Services.BinaryMessageEncoderService, IBinaryMessageEncoderService>(ServiceHelper.GetCustomServerBinding(Channels.CompressionFormat.GZip, CommonConstants.HttpTransport, TransferMode.Buffered), "/BasicWcfService/basichttp.svc/bothEnabledBuffered");
                    builder.AddServiceEndpoint<Services.BinaryMessageEncoderService, IBinaryMessageEncoderService>(ServiceHelper.GetCustomServerBinding(Channels.CompressionFormat.Deflate, CommonConstants.HttpTransport, TransferMode.Streamed), "/BasicWcfService/basichttp.svc/bothEnabledStreamed");
                    builder.AddServiceEndpoint<Services.BinaryMessageEncoderService, IBinaryMessageEncoderService>(ServiceHelper.GetCustomServerBinding(Channels.CompressionFormat.None, CommonConstants.HttpTransport, TransferMode.Buffered), "/BasicWcfService/basichttp.svc/serviceDisabledBuffered");
                });
            }
        }
    }
}
