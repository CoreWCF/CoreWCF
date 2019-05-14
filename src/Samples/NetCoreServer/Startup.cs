using CoreWCF;
using CoreWCF.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace NetCoreServer
{
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
                builder.AddService<EchoService>();
                builder.AddServiceEndpoint<EchoService, Contract.IEchoService>(new BasicHttpBinding(), "/basichttp");
                builder.AddServiceEndpoint<EchoService, Contract.IEchoService>(new NetTcpBinding(), "/nettcp");
            });
        }
    }
}
