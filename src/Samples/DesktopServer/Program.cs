using System;
using System.Collections.Generic;
using System.IdentityModel.Selectors;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace DesktopServer
{
    class Program
    {
        static void Main(string[] args)
        {
            ServicePointManager.ServerCertificateValidationCallback += new RemoteCertificateValidationCallback(ValidateCertificate);

            var host = new ServiceHost(typeof(EchoService), 
                new Uri("net.tcp://localhost:8808/")
               // , new Uri("http://localhost:8080/")
                );
            NetTcpBinding tcpBind = new NetTcpBinding(SecurityMode.TransportWithMessageCredential);
            tcpBind.Security.Message.ClientCredentialType = MessageCredentialType.UserName;
            host.Credentials.ServiceCertificate.SetCertificate(
                StoreLocation.LocalMachine,
                StoreName.Root, X509FindType.FindBySubjectName
                , "localhost"
                );
            host.Credentials.UserNameAuthentication.UserNamePasswordValidationMode = System.ServiceModel.Security.UserNamePasswordValidationMode.Custom;
            host.Credentials.UserNameAuthentication.CustomUserNamePasswordValidator = new TestValidator();

            host.AddServiceEndpoint(typeof(Contract.IEchoService), tcpBind, "/nettcp");
          //  host.AddServiceEndpoint(typeof(Contract.IEchoService), new BasicHttpBinding(), "/basichttp");
            host.Open();
            foreach(var endpoint in host.Description.Endpoints)
            {
                Console.WriteLine("Listening on " + endpoint.ListenUri.ToString());
            }
            Console.WriteLine("Hit enter to close");
            Console.ReadLine();
            host.Close();
        }

        private static bool ValidateCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }
    }

    internal class TestValidator : UserNamePasswordValidator
    {
        public override void Validate(string userName, string password)
        {
            Console.WriteLine("UN = " + userName);
            //throw new NotImplementedException();
        }
    }
}
