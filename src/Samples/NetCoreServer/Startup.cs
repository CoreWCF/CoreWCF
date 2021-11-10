using System;
using CoreWCF;
using CoreWCF.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using CoreWCF.Samples.StandardCommon;
using CoreWCF.Channels;

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
            services.AddSingleton<ServerLogic.EchoService, ServerLogic.EchoService>();
            services.AddLogging();
            services.AddServiceModelServices();
        }

        public static CustomBinding CreateCustomBinding()
        {
            CustomBinding result = new();
            TextMessageEncodingBindingElement textBindingElement = new()
            {
                //System.ArgumentException : Addressing Version 'AddressingNone (http://schemas.microsoft.com/ws/2005/05/addressing/none)' is not supported. (Parameter 'addressingVersion')
                MessageVersion = MessageVersion.CreateVersion(EnvelopeVersion.Soap12, AddressingVersion.None)
            };
            result.Elements.Add(textBindingElement);
            HttpTransportBindingElement httpBindingElement = new()
            {
                //AllowCookies = true,
                MaxBufferSize = int.MaxValue,
                MaxReceivedMessageSize = int.MaxValue
            };
            result.Elements.Add(httpBindingElement);
            return result;
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseServiceModel(builder =>
            {
                WSHttpBinding GetTransportWithMessageCredentialBinding()
                {
                    var serverBindingHttpsUserPassword = new WSHttpBinding(SecurityMode.TransportWithMessageCredential);
                    serverBindingHttpsUserPassword.Security.Message.ClientCredentialType = MessageCredentialType.UserName;
                    return serverBindingHttpsUserPassword;
                }

                builder.ConfigureServiceHostBase<ServerLogic.EchoService>(CustomUserNamePasswordValidatorCore.AddToHost);

                void ConfigureSoapService<TService, TContract>(string serviceprefix) where TService : class
                {
                    void AddEndpoint(CoreWCF.Channels.Binding binding, Uri address) =>
                        builder.AddServiceEndpoint<TService, TContract>(ApplyDebugTimeouts(binding), address.LocalPath);

                    var customBinding = CreateCustomBinding();

                    Settings settings = new Settings().SetDefaults("localhost", serviceprefix);
                    builder.AddService<TService>();
                    AddEndpoint(GetTransportWithMessageCredentialBinding(), settings.wsHttpAddressValidateUserPassword);
                    AddEndpoint(customBinding, settings.CustomAddress);
                    AddEndpoint(new BasicHttpBinding(), settings.basicHttpAddress);
                    AddEndpoint(new WSHttpBinding(SecurityMode.None), settings.wsHttpAddress);
                    AddEndpoint(new WSHttpBinding(SecurityMode.Transport), settings.wsHttpsAddress);
                    AddEndpoint(new NetTcpBinding(),settings.netTcpAddress);
                }

                ConfigureSoapService<ServerLogic.EchoService, Contract.IEchoService>(nameof(ServerLogic.EchoService));
            });
        }
    }
}
