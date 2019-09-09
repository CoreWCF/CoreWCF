﻿using CoreWCF.Configuration;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.ServiceModel.Channels;
using System.Text;
using System.Threading;
using Xunit;

public static class ConnectionHandlerBufferedModeTests
{
    [Fact]
    public static void SimpleNetTcpClientConnection()
    {
        string testString = new string('a', 3000);
        var host = CreateWebHostBuilder(new string[0]).Build();
        using (host)
        {
            host.Start();
            var binding = new System.ServiceModel.NetTcpBinding();
            var factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(binding,
                new System.ServiceModel.EndpointAddress(new Uri("net.tcp://localhost:8808/nettcp.svc")));
            var channel = factory.CreateChannel();
            ((IChannel)channel).Open();
            var result = channel.EchoString(testString);
            ((IChannel)channel).Close();
            Assert.Equal(testString, result);
        }
    }

    [Fact]
    public static void MultipleClientsNonConcurrentNetTcpClientConnection()
    {
        string testString = new string('a', 3000);
        var host = CreateWebHostBuilder(new string[0]).Build();
        using (host)
        {
            host.Start();
            var binding = new System.ServiceModel.NetTcpBinding();
            var factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(binding,
                new System.ServiceModel.EndpointAddress(new Uri("net.tcp://localhost:8808/nettcp.svc")));
            var channel = factory.CreateChannel();
            ((IChannel)channel).Open();
            var result = channel.EchoString(testString);
            ((IChannel)channel).Close();
            Assert.Equal(testString, result);
            channel = factory.CreateChannel();
            ((IChannel)channel).Open();
            result = channel.EchoString(testString);
            ((IChannel)channel).Close();
            Assert.Equal(testString, result);
        }
    }

    [Fact]
    public static void ConcurrentNetTcpClientConnection()
    {
        string testString = new string('a', 3000);
        var host = CreateWebHostBuilder(new string[0]).Build();
        using (host)
        {
            host.Start();
            var binding = new System.ServiceModel.NetTcpBinding();
            var factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(binding,
                new System.ServiceModel.EndpointAddress(new Uri("net.tcp://localhost:8808/nettcp.svc")));
            var channel = factory.CreateChannel();
            ((IChannel)channel).Open();
            var resultTask = channel.WaitForSecondRequestAsync();
            Thread.Sleep(TimeSpan.FromSeconds(1));
            channel.SecondRequest();
            var waitResult = resultTask.GetAwaiter().GetResult();
            Assert.True(waitResult, $"SecondRequest wasn't executed concurrently");
        }
    }

    [Fact]
    public static void MultipleClientsUsingPooledSocket()
    {
        var host = CreateWebHostBuilder(new string[0]).Build();
        using (host)
        {
            host.Start();
            var binding = new System.ServiceModel.NetTcpBinding()
            {
                OpenTimeout = TimeSpan.FromMinutes(20),
                CloseTimeout = TimeSpan.FromMinutes(20),
                SendTimeout = TimeSpan.FromMinutes(20),
                ReceiveTimeout = TimeSpan.FromMinutes(20)
            };
            var factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(binding,
                new System.ServiceModel.EndpointAddress(new Uri("net.tcp://localhost:8808/nettcp.svc")));
            var channel = factory.CreateChannel();
            ((IChannel)channel).Open();
            var clientIpEndpoint = channel.GetClientIpEndpoint();
            ((IChannel)channel).Close();
            for(int i = 0; i< 10; i++)
            {
                channel = factory.CreateChannel();
                ((IChannel)channel).Open();
                var clientIpEndpoint2 = channel.GetClientIpEndpoint();
                ((IChannel)channel).Close();
                Assert.Equal(clientIpEndpoint, clientIpEndpoint2);
            }
        }
    }

    [Fact]
    public static void MessageContract()
    {
        var host = CreateWebHostBuilder(new string[0]).Build();
        using (host)
        {
            host.Start();
            var binding = new System.ServiceModel.NetTcpBinding();
            var factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(binding,
                new System.ServiceModel.EndpointAddress(new Uri("net.tcp://localhost:8808/nettcp.svc")));
            var channel = factory.CreateChannel();
            ((IChannel)channel).Open();

            var message = new ClientContract.TestMessage()
            {
                Header = "Header",
                Body = new MemoryStream(Encoding.UTF8.GetBytes("Hello world"))
            };
            var result = channel.TestMessageContract(message);
            ((IChannel)channel).Close();
            Assert.Equal("Header from server", result.Header);
            Assert.Equal("Hello world from server", new StreamReader(result.Body, Encoding.UTF8).ReadToEnd());
        }
    }

    public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
        WebHost.CreateDefaultBuilder(args)
        .UseNetTcp(8808)
        .UseStartup<Startup>();

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
