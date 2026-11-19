using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.ServiceModel.Channels;
using Xunit;
using Xunit.Abstractions;

namespace BasicPortSharing
{
    public class BasicPortSharing
    {
        private const string NetTcpServiceBaseUri = "net.tcp://localhost:8808";
        private const string BasicPortSharingNetTcpServiceUri = NetTcpServiceBaseUri + Startup.BufferedRelatveAddress;
        private ITestOutputHelper _output;

        public BasicPortSharing(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [InlineData("ServerMultiplex")]
        [InlineData("ServerDuplex")]
        [InlineData("ServerSimplex")]
        public void PortSharing(string serviceType)
        {
            Startup.serviceName = serviceType;
            var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {                
                host.Start();
                switch (serviceType)
                {
                    case "ServerMultiplex":
                        this.ServerMultiplex();
                        break;
                    case "ServerDuplex":
                        this.ServerDuplex();
                        break;
                    case "ServerSimplex":
                        this.ServerSimplex();
                        break;
                    default:
                        break;
                }
            }
            
        }

        private void ServerMultiplex()
        {
            System.ServiceModel.ChannelFactory<ServiceContract.IMultiplexService> factory = null;
            ServiceContract.IMultiplexService channel = null;
            try
            {
                var binding = ClientHelper.GetBufferedModeBinding();
                factory = new System.ServiceModel.ChannelFactory<ServiceContract.IMultiplexService>(binding,
                    new System.ServiceModel.EndpointAddress(new Uri(BasicPortSharingNetTcpServiceUri)));
                _output.WriteLine("Client before creating first duplex channel");
                channel = factory.CreateChannel();
                ((IChannel)channel).Open();
                _output.WriteLine("Client before sending first duplex channel");
                channel.MultiplexMethod("Duplex");
                _output.WriteLine("Client Passed");
                ((IChannel)channel).Close();
                factory.Close();
            }
            finally
            {
                ServiceHelper.CloseServiceModelObjects((IChannel)channel, factory);
            }
        }

        private void ServerSimplex()
        {
            System.ServiceModel.ChannelFactory<ServiceContract.ISimplexService> factory = null;
            ServiceContract.ISimplexService channel = null;
            try
            {
                var binding = ClientHelper.GetBufferedModeBinding();
                factory = new System.ServiceModel.ChannelFactory<ServiceContract.ISimplexService>(binding,
                    new System.ServiceModel.EndpointAddress(new Uri(BasicPortSharingNetTcpServiceUri)));
                _output.WriteLine("Client before creating first duplex channel");
                channel = factory.CreateChannel();
                ((IChannel)channel).Open();
                _output.WriteLine("Client before sending first duplex channel");
                channel.SimplexMethod("simplex");
                _output.WriteLine("Client Passed");
                ((IChannel)channel).Close();
                factory.Close();
            }
            finally
            {
                ServiceHelper.CloseServiceModelObjects((IChannel)channel, factory);
            }
        }

        private void ServerDuplex()
        {
            System.ServiceModel.ChannelFactory<ServiceContract.IDuplexService> factory = null;
            ServiceContract.IDuplexService channel = null;
            try
            {
                var binding = ClientHelper.GetBufferedModeBinding();
                factory = new System.ServiceModel.ChannelFactory<ServiceContract.IDuplexService>(binding,
                    new System.ServiceModel.EndpointAddress(new Uri(BasicPortSharingNetTcpServiceUri)));
                _output.WriteLine("Client before creating first duplex channel");
                channel = factory.CreateChannel();
                ((IChannel)channel).Open();
                _output.WriteLine("Client before sending first duplex channel");
                string result = channel.DuplexMethod("Duplex");
                _output.WriteLine("Client Passed");
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
        public const string BufferedRelatveAddress = "/nettcp.svc/Buffered";
        public const string StreamedRelatveAddress = "/nettcp.svc/Streamed";
        public static string serviceName;

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddServiceModelServices();
        }
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseServiceModel(builder =>
            {
                switch (serviceName)
                {
                    case "ServerDuplex":
                        builder.AddService<Services.ServerDuplex>();
                        builder.AddServiceEndpoint<Services.ServerDuplex, ServiceContract.IDuplexService>(new CoreWCF.NetTcpBinding(CoreWCF.SecurityMode.None), BufferedRelatveAddress);
                        builder.AddServiceEndpoint<Services.ServerDuplex, ServiceContract.IDuplexService>(
                            new CoreWCF.NetTcpBinding(CoreWCF.SecurityMode.None)
                            {
                                TransferMode = CoreWCF.TransferMode.Streamed
                            }, StreamedRelatveAddress);
                        break;
                    case "ServerMultiplex":
                        builder.AddService<Services.ServerMultiplex>();
                        builder.AddServiceEndpoint<Services.ServerMultiplex, ServiceContract.IMultiplexService>(new CoreWCF.NetTcpBinding(CoreWCF.SecurityMode.None), BufferedRelatveAddress);
                        builder.AddServiceEndpoint<Services.ServerMultiplex, ServiceContract.IMultiplexService>(
                            new CoreWCF.NetTcpBinding(CoreWCF.SecurityMode.None)
                            {
                                TransferMode = CoreWCF.TransferMode.Streamed
                            }, StreamedRelatveAddress);
                        break;
                    case "ServerSimplex":
                        builder.AddService<Services.ServerSimplex>();
                        builder.AddServiceEndpoint<Services.ServerSimplex, ServiceContract.ISimplexService>(new CoreWCF.NetTcpBinding(CoreWCF.SecurityMode.None), BufferedRelatveAddress);
                        builder.AddServiceEndpoint<Services.ServerSimplex, ServiceContract.ISimplexService>(
                            new CoreWCF.NetTcpBinding(CoreWCF.SecurityMode.None)
                            {
                                TransferMode = CoreWCF.TransferMode.Streamed
                            }, StreamedRelatveAddress);
                        break;
                    default:
                        break;
                }
            });
        }
    }
}
