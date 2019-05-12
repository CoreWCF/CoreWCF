using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using CoreWCF.Configuration;


namespace NetTcpEchoServiceSample
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddServiceModelServices();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app)
        {
            app.UseServiceModel(builder =>
            {
                builder.AddService<EchoService>();
                builder.AddServiceEndpoint<EchoService, IEchoService>(new CoreWCF.NetTcpBinding(), "/nettcp.svc");
            });
        }
    }
}
