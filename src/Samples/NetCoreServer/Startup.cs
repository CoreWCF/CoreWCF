using System;
using CoreWCF;
using CoreWCF.Configuration;
using CoreWCF.IdentityModel.Selectors;
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

        //public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        //{
        //    app.UseServiceModel(builder =>
        //    {
        //        builder
        //            .AddService<EchoService>()
        //            .AddServiceEndpoint<EchoService, Contract.IEchoService>(new BasicHttpBinding(), "/basichttp")
        //            .AddServiceEndpoint<EchoService, Contract.IEchoService>(new NetTcpBinding(), "/nettcp");
        //    });
        //}

        public void ChangeHostBehavior(ServiceHostBase host)
        {
            var srvCredentials = new CoreWCF.Description.ServiceCredentials();
            srvCredentials.ServiceCertificate.Certificate = GetServiceCertificate();
            srvCredentials.UserNameAuthentication.UserNamePasswordValidationMode
            = CoreWCF.Security.UserNamePasswordValidationMode.Custom;
            srvCredentials.UserNameAuthentication.CustomUserNamePasswordValidator
                = new CustomTestValidator();
            host.Description.Behaviors.Add(srvCredentials);
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            CoreWCF.NetTcpBinding serverBinding = new CoreWCF.NetTcpBinding(SecurityMode.TransportWithMessageCredential);
            serverBinding.Security.Message.ClientCredentialType = MessageCredentialType.UserName;
            app.UseServiceModel(builder =>
            {
                WSHttpBinding GetTransportWithMessageCredentialBinding ()
                {
                    var serverBindingHttpsUserPassword = new WSHttpBinding(SecurityMode.TransportWithMessageCredential);
                    serverBindingHttpsUserPassword.Security.Message.ClientCredentialType = MessageCredentialType.UserName;
                    return serverBindingHttpsUserPassword;
                }

                builder.ConfigureServiceHostBase<EchoService>(CustomUserNamePasswordValidatorCore.AddToHost);

                void ConfigureSoapService<TService,TContract>(string serviceprefix) where TService : class
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
            });
        }
    }

}
