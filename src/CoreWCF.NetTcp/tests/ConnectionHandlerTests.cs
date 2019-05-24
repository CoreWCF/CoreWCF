using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using CoreWCF.Channels.Framing;
using CoreWCF.Configuration;
using System;
using System.Collections.Generic;
using System.ServiceModel.Channels;
using System.Text;
using Xunit;

public static class ConnectionHandlerTests
{
    [Fact]
    public static void NetTcpClientConnection()
    {
        string testString = new string('a', 3000);
        var host = CreateWebHostBuilder(new string[0]).Build();
        using (host)
        {
            host.Start();
            var binding = new System.ServiceModel.NetTcpBinding();
            var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(binding,
                new System.ServiceModel.EndpointAddress(new Uri("net.tcp://localhost:8808/nettcp.svc")));
            var channel = factory.CreateChannel();
            ((IChannel)channel).Open();
            var result = channel.EchoString(testString);
            Assert.Equal(testString, result);
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

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseServiceModel(builder =>
            {
                builder.AddService<Services.EchoService>();
                builder.AddServiceEndpoint<Services.EchoService, ServiceContract.IEchoService>(new CoreWCF.NetTcpBinding(), "/nettcp.svc");
            });
            app.Run(async (context) =>
            {
                await context.Response.WriteAsync("Hello World!");
            });
        }
    }
}
