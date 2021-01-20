using CoreWCF.Configuration;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using System.Net;

namespace NetCoreServer
{
    class Program
    {   
        static void Main(string[] args)
        {
            var host = CreateWebHostBuilder(args).Build();
            host.Run();
        }

        //public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
        //    WebHost.CreateDefaultBuilder(args)
        //    .UseKestrel(options => { options.ListenLocalhost(8000); })
        //    .UseUrls("https://localhost:8000")
        //    .UseNetTcp(8808)
        //    .UseStartup<Startup>();

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
    WebHost.CreateDefaultBuilder(args)
    .UseKestrel(
        //options => {
        //    options.ListenAnyIP(8000);
        //    options.Listen(address: IPAddress.Loopback, 8000,
        //                listenOptions =>
        //    {
        //        listenOptions.UseHttps();
        //    });
    //    }
    )
    .UseUrls("https://wcfserv.mscore.local:8000", "http://wcfserv.mscore.local:9080")
   // .UseNetTcp(9808)
    .UseStartup<Startup>();
    }
}
