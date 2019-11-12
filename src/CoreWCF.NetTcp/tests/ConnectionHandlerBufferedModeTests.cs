﻿using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading;
using Xunit;
using Xunit.Abstractions;

namespace ConnectionHandler
{
    public class ConnectionHandlerBufferedModeTests
    {
        private const string NetTcpServiceUri = "net.tcp://localhost:8808/nettcp.svc";
        private ITestOutputHelper _output;

        public ConnectionHandlerBufferedModeTests(ITestOutputHelper output)
        {
            _output = output;
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
                    var binding = ClientHelper.GetBufferedModeBinding();
                    factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(binding,
                        new System.ServiceModel.EndpointAddress(new Uri(NetTcpServiceUri)));
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
                    var binding = ClientHelper.GetBufferedModeBinding();
                    factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(binding,
                        new System.ServiceModel.EndpointAddress(new Uri(NetTcpServiceUri)));
                    channel = factory.CreateChannel();
                    ((IChannel)channel).Open();
                    var result = channel.EchoString(testString);
                    ((IChannel)channel).Close();
                    Assert.Equal(testString, result);
                    channel = factory.CreateChannel();
                    ((IChannel)channel).Open();
                    result = channel.EchoString(testString);
                    ((IChannel)channel).Close();
                    Assert.Equal(testString, result);
                    factory.Close();
                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((IChannel)channel, factory);
                }
            }
        }

        [Fact]
        public void ConcurrentNetTcpClientConnection()
        {
            var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                System.ServiceModel.ChannelFactory<ClientContract.ITestService> factory = null;
                ClientContract.ITestService channel = null;
                host.Start();
                try
                {
                    var binding = ClientHelper.GetBufferedModeBinding();
                    factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(binding,
                        new System.ServiceModel.EndpointAddress(new Uri(NetTcpServiceUri)));
                    channel = factory.CreateChannel();
                    ((IChannel)channel).Open();
                    var resultTask = channel.WaitForSecondRequestAsync();
                    Thread.Sleep(TimeSpan.FromSeconds(1));
                    channel.SecondRequest();
                    var waitResult = resultTask.GetAwaiter().GetResult();
                    Assert.True(waitResult, $"SecondRequest wasn't executed concurrently");
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
                    var binding = ClientHelper.GetBufferedModeBinding();
                    factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(binding,
                        new System.ServiceModel.EndpointAddress(new Uri(NetTcpServiceUri)));
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
        public void MessageContract()
        {
            var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                System.ServiceModel.ChannelFactory<ClientContract.ITestService> factory = null;
                ClientContract.ITestService channel = null;
                host.Start();
                try
                {
                    var binding = ClientHelper.GetBufferedModeBinding();
                    factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(binding,
                        new System.ServiceModel.EndpointAddress(new Uri(NetTcpServiceUri)));
                    channel = factory.CreateChannel();
                    ((IChannel)channel).Open();
                    var message = new ClientContract.TestMessage()
                    {
                        Header = "Header",
                        Body = new MemoryStream(Encoding.UTF8.GetBytes("Hello world"))
                    };
                    var result = channel.TestMessageContract(message);
                    Assert.Equal("Header from server", result.Header);
                    Assert.Equal("Hello world from server", new StreamReader(result.Body, Encoding.UTF8).ReadToEnd());
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
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app, IHostingEnvironment env)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.TestService>();
                    builder.AddServiceEndpoint<Services.TestService, ServiceContract.ITestService>(new CoreWCF.NetTcpBinding(), "/nettcp.svc");
                });
            }
        }
    }
}