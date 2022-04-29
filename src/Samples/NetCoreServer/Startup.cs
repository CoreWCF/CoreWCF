using System;
using CoreWCF;
using CoreWCF.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Contract;

namespace NetCoreServer
{
    public class Startup
    {
        public const int HTTP_PORT = 8088;
        public const int HTTPS_PORT = 8443;
        public const int NETTCP_PORT = 8089;


        public void ConfigureServices(IServiceCollection services)
        {
            //Enable CoreWCF Services, with metadata (WSDL) support
            services.AddServiceModelServices()
                    .AddServiceModelMetadata();
        }

        public void Configure(IApplicationBuilder app)
        {
             app.UseServiceModel(builder =>
            {
                // Add the Echo Service
                builder.AddService<EchoService>(serviceOptions =>
                {
                    // Set a base address for all bindings to the service, and WSDL discovery
                    serviceOptions.BaseAddresses.Add(new Uri($"http://localhost:{HTTP_PORT}/EchoService"));
                    serviceOptions.BaseAddresses.Add(new Uri($"https://localhost:{HTTPS_PORT}/EchoService"));
                })
                // Add a BasicHttpBinding endpoint
                .AddServiceEndpoint<EchoService, IEchoService>(new BasicHttpBinding(), "/basichttp")

                // Add WSHttpBinding endpoints
                .AddServiceEndpoint<EchoService, IEchoService>(new WSHttpBinding(SecurityMode.None), "/wsHttp")
                .AddServiceEndpoint<EchoService, IEchoService>(new WSHttpBinding(SecurityMode.Transport), "/wsHttp")
 
                // Add NetTcpBinding
                .AddServiceEndpoint<EchoService, IEchoService>(new NetTcpBinding(), $"net.tcp://localhost:{NETTCP_PORT}/nettcp");

                // Configure WSDL to be available over http & https
                var serviceMetadataBehavior = app.ApplicationServices.GetRequiredService<CoreWCF.Description.ServiceMetadataBehavior>();
                serviceMetadataBehavior.HttpGetEnabled = true;
                serviceMetadataBehavior.HttpsGetEnabled = true;
            });
        }

        private static readonly TimeSpan s_debugTimeout = TimeSpan.FromMinutes(20);

        private static CoreWCF.Channels.Binding ApplyDebugTimeouts(CoreWCF.Channels.Binding binding)
        {
            if (System.Diagnostics.Debugger.IsAttached)
            {
                binding.OpenTimeout =
                    binding.CloseTimeout =
                    binding.SendTimeout =
                    binding.ReceiveTimeout = s_debugTimeout;
            }
            return binding;
        }

    }

}
