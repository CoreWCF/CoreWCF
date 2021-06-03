using System;
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

        private static readonly TimeSpan s_debugTimeout = TimeSpan.FromMinutes(20);

        private static void ApplyDebugTimeouts(CoreWCF.Channels.Binding binding)
        {
            if (System.Diagnostics.Debugger.IsAttached)
            {
                binding.OpenTimeout =
                    binding.CloseTimeout =
                    binding.SendTimeout =
                    binding.ReceiveTimeout = s_debugTimeout;
            }
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseServiceModel(builder =>
            {
                var serverBinding = new WSHttpBinding(SecurityMode.None);
                serverBinding.Security.Message.ClientCredentialType = MessageCredentialType.None;

                var serverBindingHttps = new WSHttpBinding(SecurityMode.Transport);
                serverBindingHttps.Security.Message.ClientCredentialType = MessageCredentialType.None;

                var serverBindingHttpsUserPassword = new WSHttpBinding(SecurityMode.TransportWithMessageCredential);
                ApplyDebugTimeouts(serverBindingHttpsUserPassword);

                serverBindingHttpsUserPassword.Security.Message.ClientCredentialType = MessageCredentialType.UserName;

                builder.ConfigureServiceHostBase<EchoService>(CustomUserNamePasswordValidatorCore.AddToHost);

                builder
                    .AddService<EchoService>()
                    .AddServiceEndpoint<EchoService, Contract.IEchoService>(serverBindingHttpsUserPassword, "/wsHttpUserPassword.svc")
                    .AddServiceEndpoint<EchoService, Contract.IEchoService>(new BasicHttpBinding(), "/basichttp")
                    .AddServiceEndpoint<EchoService, Contract.IEchoService>(serverBinding, "/wsHttp.svc")
                    .AddServiceEndpoint<EchoService, Contract.IEchoService>(serverBindingHttps, "/wsHttp.svc")
                    .AddServiceEndpoint<EchoService, Contract.IEchoService>(new NetTcpBinding(), "/nettcp");
            });
        }
    }

}
