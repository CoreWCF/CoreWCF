using System;
using System.Security.Cryptography.X509Certificates;
using CoreWCF;
using CoreWCF.Configuration;
using CoreWCF.IdentityModel.Selectors;
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
                builder.AddService<EchoService>();
                builder.AddServiceEndpoint<EchoService, Contract.IEchoService>(serverBinding, "/nettcp");
                Action<ServiceHostBase> serviceHost = host => ChangeHostBehavior(host);
                builder.ConfigureServiceHostBase<EchoService>(serviceHost);
            });
        }

        internal class CustomTestValidator : UserNamePasswordValidator
        {
            public override void Validate(string userName, string password)
            {
                if (string.Compare(userName, "testuser@corewcf", StringComparison.OrdinalIgnoreCase) == 0 && password.Length > 0)
                {
                    //Write custom logic
                    return;
                }
                else
                {
                    throw new Exception("Permission Denied");
                }
            }
        }

        public static X509Certificate2 GetServiceCertificate()
        {
            string AspNetHttpsOid = "1.3.6.1.4.1.311.84.1.1";
            X509Certificate2 foundCert = null;
            using (var store = new X509Store(StoreName.My, StoreLocation.CurrentUser))
            {
                // X509Store.Certificates creates a new instance of X509Certificate2Collection with
                // each access to the property. The collection needs to be cleaned up correctly so
                // keeping a single reference to fetched collection.
                store.Open(OpenFlags.ReadOnly);
                var certificates = store.Certificates;
                foreach (var cert in certificates)
                {
                    foreach (var extension in cert.Extensions)
                    {
                        if (AspNetHttpsOid.Equals(extension.Oid?.Value))
                        {
                            // Always clone certificate instances when you don't own the creation
                            foundCert = new X509Certificate2(cert);
                            break;
                        }
                    }

                    if (foundCert != null)
                    {
                        break;
                    }
                }
                // Cleanup
                foreach (var cert in certificates)
                {
                    cert.Dispose();
                }
            }

            return foundCert;
        }
    }
}

