using CoreWCF.Configuration;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.ServiceModel.Channels;
using System.Threading;
using Xunit;

public static class ConnectionHandlerStreamedModeTests
{
    [Fact]
    public static void SimpleNetTcpClientConnection()
    {
        string testString = new string('a', 3000);
        var host = CreateWebHostBuilder(new string[0]).Build();
        using (host)
        {
            host.Start();
            var binding = new System.ServiceModel.NetTcpBinding
            {
                TransferMode = System.ServiceModel.TransferMode.Streamed
            };
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
            var binding = new System.ServiceModel.NetTcpBinding
            {
                TransferMode = System.ServiceModel.TransferMode.Streamed
            };
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
    public static void SingleClientMultipleRequestsNetTcpClientConnection()
    {
        string testString = new string('a', 3000);
        var host = CreateWebHostBuilder(new string[0]).Build();
        using (host)
        {
            host.Start();
            var binding = new System.ServiceModel.NetTcpBinding
            {
                TransferMode = System.ServiceModel.TransferMode.Streamed
            };
            var factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(binding,
                new System.ServiceModel.EndpointAddress(new Uri("net.tcp://localhost:8808/nettcp.svc")));
            var channel = factory.CreateChannel();
            ((IChannel)channel).Open();
            var result = channel.EchoString(testString);
            Assert.Equal(testString, result);
            result = channel.EchoString(testString);
            ((IChannel)channel).Close();
            Assert.Equal(testString, result);
        }
    }

    //[Fact]
    //public static void ConcurrentNetTcpClientConnection()
    //{
    //    string testString = new string('a', 3000);
    //    var host = CreateWebHostBuilder(new string[0]).Build();
    //    using (host)
    //    {
    //        host.Start();
    //        var binding = new System.ServiceModel.NetTcpBinding();
    //        var factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(binding,
    //            new System.ServiceModel.EndpointAddress(new Uri("net.tcp://localhost:8808/nettcp.svc")));
    //        var channel = factory.CreateChannel();
    //        ((IChannel)channel).Open();
    //        var resultTask = channel.WaitForSecondRequestAsync();
    //        Thread.Sleep(TimeSpan.FromSeconds(1));
    //        channel.SecondRequest();
    //        var waitResult = resultTask.GetAwaiter().GetResult();
    //        Assert.True(waitResult, $"SecondRequest wasn't executed concurrently");
    //    }
    //}

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
                builder.AddServiceEndpoint<Services.TestService, ServiceContract.ITestService>(
                    new CoreWCF.NetTcpBinding
                    {
                        TransferMode = CoreWCF.TransferMode.Streamed
                    }, "/nettcp.svc");
            });
        }
    }
}
