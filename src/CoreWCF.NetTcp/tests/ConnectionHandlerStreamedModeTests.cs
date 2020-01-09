using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.ServiceModel.Channels;
using Xunit;
using Xunit.Abstractions;

namespace ConnectionHandler
{
    public class ConnectionHandlerStreamedModeTests
    {
        private const string NetTcpServiceBaseUri = "net.tcp://localhost:8808";
        private const string WindowsAuthNetTcpServiceUri = NetTcpServiceBaseUri + Startup.WindowsAuthRelativePath;
        private const string NoAuthNetTcpServiceUri = NetTcpServiceBaseUri + Startup.NoSecurityRelativePath;

        private ITestOutputHelper _output;

        public ConnectionHandlerStreamedModeTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        [Trait("Category", "WindowsOnly")]
        public void SimpleNetTcpClientConnectionWindowsAuth()
        {
            string testString = new string('a', 3000);
            var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                System.ServiceModel.ChannelFactory<ClientContract.ITestService> factory = null;
                ClientContract.ITestService channel = null;
                host.Start();
                try
                {
                    var binding = ClientHelper.GetStreamedModeBinding(System.ServiceModel.SecurityMode.Transport);
                    factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(binding,
                        new System.ServiceModel.EndpointAddress(new Uri(WindowsAuthNetTcpServiceUri)));
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

        [Fact]
        public void SimpleNetTcpClientConnection()
        {
            string testString = new string('a', 3000);
            var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                System.ServiceModel.ChannelFactory<ClientContract.ITestService> factory = null;
                ClientContract.ITestService channel = null;
                host.Start();
                try
                {
                    var binding = ClientHelper.GetStreamedModeBinding();
                    factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(binding,
                        new System.ServiceModel.EndpointAddress(new Uri(NoAuthNetTcpServiceUri)));
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

        [Fact]
        public void MultipleClientsNonConcurrentNetTcpClientConnection()
        {
            string testString = new string('a', 3000);
            var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                System.ServiceModel.ChannelFactory<ClientContract.ITestService> factory = null;
                ClientContract.ITestService channel = null;
                host.Start();
                try
                {
                    var binding = ClientHelper.GetStreamedModeBinding();
                    factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(binding,
                        new System.ServiceModel.EndpointAddress(new Uri(NoAuthNetTcpServiceUri)));
                    channel = factory.CreateChannel();
                    ((IChannel)channel).Open();
                    var result = channel.EchoString(testString);
                    Assert.Equal(testString, result);
                    ((IChannel)channel).Close();
                    channel = factory.CreateChannel();
                    ((IChannel)channel).Open();
                    result = channel.EchoString(testString);
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

        [Fact]
        public void SingleClientMultipleRequestsNetTcpClientConnection()
        {
            string testString = new string('a', 3000);
            var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                System.ServiceModel.ChannelFactory<ClientContract.ITestService> factory = null;
                ClientContract.ITestService channel = null;
                host.Start();
                try
                {
                    var binding = ClientHelper.GetStreamedModeBinding();
                    factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(binding,
                        new System.ServiceModel.EndpointAddress(new Uri(NoAuthNetTcpServiceUri)));
                    channel = factory.CreateChannel();
                    ((IChannel)channel).Open();
                    var result = channel.EchoString(testString);
                    Assert.Equal(testString, result);
                    result = channel.EchoString(testString);
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

        [Fact]
        public void MultipleClientsUsingPooledSocket()
        {
            var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                System.ServiceModel.ChannelFactory<ClientContract.ITestService> factory = null;
                ClientContract.ITestService channel = null;
                host.Start();
                try
                {
                    var binding = ClientHelper.GetStreamedModeBinding();
                    factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(binding,
                        new System.ServiceModel.EndpointAddress(new Uri(NoAuthNetTcpServiceUri)));
                    channel = factory.CreateChannel();
                    ((IChannel)channel).Open();
                    var clientIpEndpoint = channel.GetClientIpEndpoint();
                    ((IChannel)channel).Close();
                    for (int i = 0; i < 10; i++)
                    {
                        channel = factory.CreateChannel();
                        ((IChannel)channel).Open();
                        var clientIpEndpoint2 = channel.GetClientIpEndpoint();
                        Assert.Equal(clientIpEndpoint, clientIpEndpoint2);
                        ((IChannel)channel).Close();
                    }
                    factory.Close();
                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((IChannel)channel, factory);
                }
            }
        }

        [Fact]
        public void SingleClientsUsingPooledSocketForMultipleRequests()
        {
            var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                System.ServiceModel.ChannelFactory<ClientContract.ITestService> factory = null;
                ClientContract.ITestService channel = null;
                host.Start();
                try
                {
                    var binding = ClientHelper.GetStreamedModeBinding();
                    factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(binding,
                        new System.ServiceModel.EndpointAddress(new Uri(NoAuthNetTcpServiceUri)));
                    channel = factory.CreateChannel();
                    ((IChannel)channel).Open();
                    var clientIpEndpoint = channel.GetClientIpEndpoint();
                    for (int i = 0; i < 10; i++)
                    {
                        var clientIpEndpoint2 = channel.GetClientIpEndpoint();
                        Assert.Equal(clientIpEndpoint, clientIpEndpoint2);
                    }
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
            public const string WindowsAuthRelativePath = "/nettcp.svc/windows-auth";
            public const string NoSecurityRelativePath = "/nettcp.svc/security-none";

            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app, IHostingEnvironment env)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.TestService>();
                    builder.AddServiceEndpoint<Services.TestService, ServiceContract.ITestService>(
                        new CoreWCF.NetTcpBinding
                        {
                            TransferMode = CoreWCF.TransferMode.Streamed
                        }, WindowsAuthRelativePath);
                    builder.AddServiceEndpoint<Services.TestService, ServiceContract.ITestService>(
                        new CoreWCF.NetTcpBinding(CoreWCF.SecurityMode.None)
                        {
                            TransferMode = CoreWCF.TransferMode.Streamed
                        }, NoSecurityRelativePath);
                });
            }
        }
    }
}