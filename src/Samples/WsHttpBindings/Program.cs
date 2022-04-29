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

        // Listen on 8088 for http, and 8443 for https, 8089 for NetTcp.
        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
            .UseKestrel(options => {
                options.ListenLocalhost(WSHttpUserPassword.HTTP_PORT);
                options.ListenLocalhost(WSHttpUserPassword.HTTPS_PORT, listenOptions =>
                {
                    listenOptions.UseHttps();
                    if (Debugger.IsAttached)
                    {
                        listenOptions.UseConnectionLogging();
                    }
                });
            })

            // Replace with other WSFedBinding or WSHttpWithWindowsAuthAndRoles for other binding types
            .UseStartup<WSHttpUserPassword>();

        
    }
}
