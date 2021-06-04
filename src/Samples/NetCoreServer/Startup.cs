using System;
using CoreWCF;
using CoreWCF.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using CoreWCF.Samples.StandardCommon;

namespace NetCoreServer
{
    public class Startup
    {
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

                var serverBindingHttpsUserPassword = new WSHttpBinding(SecurityMode.TransportWithMessageCredential);
                ApplyDebugTimeouts(serverBindingHttpsUserPassword);
                serverBindingHttpsUserPassword.Security.Message.ClientCredentialType = MessageCredentialType.UserName;

                builder.ConfigureServiceHostBase<EchoService>(CustomUserNamePasswordValidatorCore.AddToHost);

                Settings settings = new Settings().SetDetaults();

                builder
                    .AddService<EchoService>()
                    .AddServiceEndpoint<EchoService, Contract.IEchoService>(
                        serverBindingHttpsUserPassword, settings.wsHttpAddressValidateUserPassword.LocalPath)
                    .AddServiceEndpoint<EchoService, Contract.IEchoService>(new BasicHttpBinding(), settings.basicHttpAddress.LocalPath)
                    .AddServiceEndpoint<EchoService, Contract.IEchoService>(serverBinding, settings.wsHttpAddress.LocalPath)
                    .AddServiceEndpoint<EchoService, Contract.IEchoService>(serverBindingHttps, settings.wsHttpsAddress.LocalPath)
                    .AddServiceEndpoint<EchoService, Contract.IEchoService>(new NetTcpBinding(), settings.netTcpAddress.LocalPath);
            });
        }
    }

}
