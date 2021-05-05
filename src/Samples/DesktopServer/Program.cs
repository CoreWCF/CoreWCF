using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel;
using CoreWCF.Samples.StandardCommon;

namespace DesktopServer
{
    class Program
    {
        private static ServiceHost ConfigureWcfHost<TService, TContract>(string servicePrefix)
        {
            Settings settings = new Settings().SetDefaults("localhost", servicePrefix);

            Uri[] baseUriList = settings.GetBaseAddresses().ToArray();

            Type contract = typeof(TContract);
            var host = new ServiceHost(typeof(TService), baseUriList);

            var serverBindingHttpsUserPassword = new WSHttpBinding(SecurityMode.TransportWithMessageCredential);
            serverBindingHttpsUserPassword.Security.Message.ClientCredentialType = MessageCredentialType.UserName;
            host.AddServiceEndpoint(contract, serverBindingHttpsUserPassword, settings.wsHttpAddressValidateUserPassword.LocalPath);
            CustomUserNamePasswordValidator.AddToHost(host);

            host.AddServiceEndpoint(contract, new BasicHttpBinding(BasicHttpSecurityMode.None), settings.basicHttpAddress.LocalPath);
            host.AddServiceEndpoint(contract, new BasicHttpsBinding(BasicHttpsSecurityMode.Transport), settings.basicHttpsAddress.LocalPath);
            host.AddServiceEndpoint(contract, new WSHttpBinding(SecurityMode.None), settings.wsHttpAddress.LocalPath);
            host.AddServiceEndpoint(contract, new WSHttpBinding(SecurityMode.Transport), settings.wsHttpsAddress.LocalPath);
            host.AddServiceEndpoint(contract, new NetTcpBinding(), settings.netTcpAddress.LocalPath);
            return host;
        }

        private static void LogHostUrls(ServiceHost host)
        {
            foreach (System.ServiceModel.Description.ServiceEndpoint endpoint in host.Description.Endpoints)
            {
                Console.WriteLine("Listening on " + endpoint.ListenUri.ToString());
            }
        }


        static void Main()
        {
            var hostEchoService = ConfigureWcfHost<EchoService, Contract.IEchoService>("EchoService");

            hostEchoService.Open();

            LogHostUrls(hostEchoService);

            Console.WriteLine("Hit enter to close");
            Console.ReadLine();

            hostEchoService.Close();
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


    internal class CustomValidator : UserNamePasswordValidator
    {
        public override void Validate(string userName, string password)
        {
            return;
        }
    }
}
