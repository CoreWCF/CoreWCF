using System;
using CoreWCF;
using CoreWCF.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Contract;

namespace NetCoreServer
{
    public class WSHttpUserPassword
    {
        public const int HTTP_PORT = 8088;
        public const int HTTPS_PORT = 8443;

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddServiceModelServices();
        }

        public void Configure(IApplicationBuilder app)
        {
            var wsHttpBindingWithCredential = new WSHttpBinding(SecurityMode.TransportWithMessageCredential);
            wsHttpBindingWithCredential.Security.Message.ClientCredentialType = MessageCredentialType.UserName;

            app.UseServiceModel(builder =>
            {
                // Add the Echo Service
                builder.AddService<EchoService>()

                // Add WSHttpBinding endpoints
                .AddServiceEndpoint<EchoService, IEchoService>(new WSHttpBinding(SecurityMode.None), "/wsHttp")
                .AddServiceEndpoint<EchoService, IEchoService>(new WSHttpBinding(SecurityMode.Transport), "/wsHttp")
                .AddServiceEndpoint<EchoService, IEchoService>(wsHttpBindingWithCredential, "/wsHttpUserPassword");

                builder.ConfigureServiceHostBase<EchoService>(CustomUserNamePasswordValidatorCore.AddToHost);
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
