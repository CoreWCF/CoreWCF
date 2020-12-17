using CoreWCF;
using CoreWCF.Configuration;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.Net;

namespace CoreWCFPerfService
{
    public class Parameters
    {
        public const string Url = "baseaddress";        
    }

    public class Program
    {
        private string _paramUrl;

        static void Main(string[] args)
        {
            Program test = new Program();

            if (test.ProcessRunOptions(args))
            {
                Uri baseAddress = new Uri(test._paramUrl);
                
                var host = CreateWebHostBuilder<Startup>(test._paramUrl,baseAddress.Scheme,baseAddress.Port).Build();
                host.Start();

                Console.WriteLine("Service is Ready");
                Console.WriteLine("Press Any Key to Terminate...");
                Console.ReadLine();
            }
        }

        public static IWebHostBuilder CreateWebHostBuilder<TStartup>(string url,string protocol,int port) where TStartup : class
        {
            var hostBuilder = WebHost.CreateDefaultBuilder(new string[0]);
            if (string.Equals(protocol, "http", StringComparison.InvariantCultureIgnoreCase))
            {
                hostBuilder = hostBuilder.UseUrls(url);
            }
            else
            {
                hostBuilder = hostBuilder.UseNetTcp(port);
            }
            
            return hostBuilder.UseStartup<TStartup>();
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
                    builder.AddService<SayHello>();
                    builder.AddServiceEndpoint<SayHello, ISayHello>(new BasicHttpBinding(), "/SayHello/SayHello.svc");
                    builder.AddServiceEndpoint<SayHello, ISayHello>(new NetTcpBinding(SecurityMode.None), "/SayHello/SayHello.svc");

                });
            }
        }

        private bool ProcessRunOptions(string[] args)
        {
            foreach (string s in args)
            {
                string[] p = s.Split(new char[] { ':' }, count: 2);
                if (p.Length != 2)
                {
                    continue;
                }

                switch (p[0].ToLower())
                {
                    case Parameters.Url:
                        _paramUrl = p[1];
                        break;

                    default:
                        Console.WriteLine("unknown argument: " + s);
                        continue;
                }
            }

            return true;
        }
    }
}
