using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using CoreWCF.Configuration;
using System;

namespace NetTcpEchoServiceSample
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var host = CreateWebHostBuilder(args).Build())
            {
                host.Start();
                var factory = new System.ServiceModel.ChannelFactory<Client.IEchoService>(
                    new System.ServiceModel.NetTcpBinding(),
                    new System.ServiceModel.EndpointAddress("net.tcp://localhost:8808/nettcp.svc"));
                factory.Open();
                var channel = factory.CreateChannel();
                ((System.ServiceModel.IClientChannel)channel).Open();
                Console.WriteLine($"Client echo'd \"{channel.EchoString("Hello")}\"");
            }
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
            .UseNetTcp(8808)
            .UseStartup<Startup>();
    }
}
