using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Xunit;

public static class SimpleTest
{
    [Fact]
    public static void BasicHttpRequestReplyEchoString()
    {
        string testString = new string('a', 3000);
        var host = CreateWebHostBuilder(new string[0]).Build();
        using (host)
        {
            host.Start();
            var httpBinding = new System.ServiceModel.BasicHttpBinding();
            var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(httpBinding,
                new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/basichttp.svc")));
            var channel = factory.CreateChannel();
            var result = channel.EchoString(testString);
            Assert.Equal(testString, result);
        }
    }

    public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
        WebHost.CreateDefaultBuilder(args)
            .UseKestrel()
            .UseUrls("http://localhost:8080")
            .UseStartup<Startup>();
}
