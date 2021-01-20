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
            WSHttpBinding wSHttpBinding = new WSHttpBinding(SecurityMode.TransportWithMessageCredential);
            wSHttpBinding.Security.Message.ClientCredentialType = MessageCredentialType.Windows;
            app.UseServiceModel(builder =>
            {
                builder.AddService<EchoService>();
                builder.AddServiceEndpoint<EchoService, Contract.IEchoService>(wSHttpBinding, "/wsHttp");
                builder.AddServiceEndpoint<EchoService, Contract.IEchoService>(new NetTcpBinding(), "/nettcp");
            });
        }
    }
}
