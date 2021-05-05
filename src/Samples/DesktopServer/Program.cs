using System;
using System.Collections.Generic;
using System.IdentityModel.Selectors;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Text;
using System.Threading.Tasks;

namespace DesktopServer
{
    class Program
    {
        static void Main(string[] args)
        {
            var host = new ServiceHost(typeof(EchoService),
                new Uri("net.tcp://localhost:8808/"));
            //  ,
            //  new Uri("https://localhost:8080/")
            //  );
            NetTcpBinding nettcp = new NetTcpBinding(SecurityMode.TransportWithMessageCredential);
            nettcp.Security.Message.ClientCredentialType = MessageCredentialType.UserName;
            nettcp.OpenTimeout = nettcp.CloseTimeout = nettcp.ReceiveTimeout = nettcp.SendTimeout
                = TimeSpan.FromMinutes(30);
            host.AddServiceEndpoint(typeof(Contract.IEchoService), nettcp, "/nettcp");
            var srvCredentials = new ServiceCredentials();
            srvCredentials.ServiceCertificate.Certificate = GetServiceCertificate();
            srvCredentials.UserNameAuthentication.UserNamePasswordValidationMode
                = System.ServiceModel.Security.UserNamePasswordValidationMode.Custom;
            srvCredentials.UserNameAuthentication.CustomUserNamePasswordValidator
                = new CustomValidator();
            host.Description.Behaviors.Add(srvCredentials);
            host.Open();

            // host.AddServiceEndpoint(typeof(Contract.IEchoService), new BasicHttpBinding(BasicHttpSecurityMode.TransportWithMessageCredential), "/basichttp");
            foreach (var endpoint in host.Description.Endpoints)
            {
                Console.WriteLine("Listening on " + endpoint.ListenUri.ToString());
            }
            Console.WriteLine("Hit enter to close");
            Console.ReadLine();
            host.Close();
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
