using System.Diagnostics;
using System.Net;
using CoreWCF.Configuration;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace NetCoreServer
{
    class Program
    {
        static void Main(string[] args)
        {
            IWebHost host = CreateWebHostBuilder(args).Build();
            host.Run();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
            .UseKestrel(options => {
                options.ListenLocalhost(8088);
                options.ListenLocalhost(8443, listenOptions =>
                {
                    listenOptions.UseHttps(httpsOptions =>
                    {
#if NET472
                        httpsOptions.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls;
#endif // NET472
                    });
                    if (Debugger.IsAttached)
                    {
                        listenOptions.UseConnectionLogging();
                    }
                });
            })
            .UseNetTcp(8089)
            .UseStartup<Startup>();
    }
}
