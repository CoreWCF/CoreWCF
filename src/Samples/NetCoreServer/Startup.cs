using CoreWCF;
using CoreWCF.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace NetCoreServer
{
    public class Startup
    {
        /*
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
        }*/
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddServiceModelServices();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseServiceModel(builder =>
            {
                var serverBinding = new WSHttpBinding(SecurityMode.None);
                serverBinding.Security.Message.ClientCredentialType = MessageCredentialType.None;

                var serverBindingHttps = new WSHttpBinding(SecurityMode.Transport);
                serverBindingHttps.Security.Message.ClientCredentialType = MessageCredentialType.None;

                builder
                    .AddService<EchoService>()
                    .AddServiceEndpoint<EchoService, Contract.IEchoService>(new BasicHttpBinding(), "/basichttp")
                    .AddServiceEndpoint<EchoService, Contract.IEchoService>(new NetTcpBinding(), "/nettcp")
                    .AddServiceEndpoint<EchoService, Contract.IEchoService>(serverBinding, "/wsHttp.svc")
                    .AddServiceEndpoint<EchoService, Contract.IEchoService>(serverBindingHttps, "/wsHttp.svc");
            });
        }
    }
}
