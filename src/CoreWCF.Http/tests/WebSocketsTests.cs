using CoreWCF;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.ServiceModel.Channels;
using Xunit;
using Xunit.Abstractions;

namespace NetHttp
{
    public class WebSocketsTests
    {
        private const string NetHttpServiceBaseUri = "http://localhost:8080";
        private const string NetHttpBufferedServiceUri = NetHttpServiceBaseUri + Startup.BufferedPath;
        private ITestOutputHelper _output;

        public WebSocketsTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void NetHttpWebSocketsBufferedTransferMode()
        {
            string testString = new string('a', 3000);
            var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                System.ServiceModel.ChannelFactory<ClientContract.IEchoService> factory = null;
                ClientContract.IEchoService channel = null;
                host.Start();
                try
                {
                    var binding = ClientHelper.GetBufferedModeWebSocketBinding();
                    factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(binding,
                        new System.ServiceModel.EndpointAddress(new Uri(NetHttpBufferedServiceUri)));
                    channel = factory.CreateChannel();
                    ((IChannel)channel).Open();
                    var result = channel.EchoString(testString);
                    Assert.Equal(testString, result);
                    ((IChannel)channel).Close();
                    factory.Close();
                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((IChannel)channel, factory);
                }
            }
        }

        //[Fact]
        //public void NetHttpWebSocketsStreamedTransferMode()
        //{
        //    string testString = new string('a', 3000);
        //    var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
        //    using (host)
        //    {
        //        System.ServiceModel.ChannelFactory<ClientContract.IEchoService> factory = null;
        //        ClientContract.IEchoService channel = null;
        //        host.Start();
        //        try
        //        {
        //            var binding = ClientHelper.GetStreamedModeWebSocketBinding();
        //            factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(binding,
        //                new System.ServiceModel.EndpointAddress(new Uri(NetHttpServiceUri)));
        //            channel = factory.CreateChannel();
        //            ((IChannel)channel).Open();
        //            var result = channel.EchoString(testString);
        //            Assert.Equal(testString, result);
        //            ((IChannel)channel).Close();
        //            factory.Close();
        //        }
        //        finally
        //        {
        //            ServiceHelper.CloseServiceModelObjects((IChannel)channel, factory);
        //        }
        //    }
        //}


        public class Startup
        {
            public const string BufferedPath = "/nethttp.svc/buffered";
            public const string StreamedPath = "/nethttp.svc/streamed";

            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app, IHostingEnvironment env)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.EchoService>();
                    var binding = new NetHttpBinding();
                    binding.WebSocketSettings.TransportUsage = CoreWCF.Channels.WebSocketTransportUsage.Always;
                    builder.AddServiceEndpoint<Services.EchoService, ServiceContract.IEchoService>(binding, BufferedPath);
                });
            }
        }
    }
}
