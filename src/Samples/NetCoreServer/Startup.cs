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
            services.AddServiceModelServices()
                    .AddServiceModelMetadata();
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            app.UseServiceModel(builder =>
            {
                WSHttpBinding GetTransportWithMessageCredentialBinding()
                {
                    var serverBindingHttpsUserPassword = new WSHttpBinding(SecurityMode.TransportWithMessageCredential);
                    serverBindingHttpsUserPassword.Security.Message.ClientCredentialType = MessageCredentialType.UserName;
                    return serverBindingHttpsUserPassword;
                }

                builder.ConfigureServiceHostBase<EchoService>(CustomUserNamePasswordValidatorCore.AddToHost);

                void ConfigureSoapService<TService, TContract>(string serviceprefix) where TService : class
                {
                    Settings settings = new Settings().SetDefaults("localhost", serviceprefix);
                    builder.AddService<TService>()
                        .AddServiceEndpoint<TService, TContract>(
                            GetTransportWithMessageCredentialBinding(), settings.wsHttpAddressValidateUserPassword.LocalPath)
                        .AddServiceEndpoint<TService, TContract>(new BasicHttpBinding(),
                            settings.basicHttpAddress.LocalPath)
                        .AddServiceEndpoint<TService, TContract>(new WSHttpBinding(SecurityMode.None),
                            settings.wsHttpAddress.LocalPath)
                        .AddServiceEndpoint<TService, TContract>(new WSHttpBinding(SecurityMode.Transport),
                            settings.wsHttpsAddress.LocalPath)
                        .AddServiceEndpoint<TService, TContract>(new NetTcpBinding(),
                            settings.netTcpAddress.LocalPath);
                }

                ConfigureSoapService<EchoService, Contract.IEchoService>(nameof(EchoService));
                var serviceMetadataBehavior = app.ApplicationServices.GetRequiredService<CoreWCF.Description.ServiceMetadataBehavior>();
                serviceMetadataBehavior.HttpGetEnabled = true;
                serviceMetadataBehavior.HttpsGetEnabled = true;
            });
        }
    }

}
