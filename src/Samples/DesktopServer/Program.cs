using System;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel;

namespace DesktopServer
{
    class Program
    {
        static void Main(string[] args)
        {
            var contract = typeof(Contract.IEchoService);
            var host = new ServiceHost(typeof(EchoService),
                new Uri("net.tcp://localhost:8089/"),
                new Uri("http://localhost:8088/"),
                new Uri("https://localhost:8443/"));

            host.AddServiceEndpoint(contract, new NetTcpBinding(), "/nettcp");
            host.AddServiceEndpoint(contract, new BasicHttpBinding(BasicHttpSecurityMode.None), "/basichttp");
            host.AddServiceEndpoint(contract, new BasicHttpsBinding(BasicHttpsSecurityMode.Transport), "/basichttp");
            host.AddServiceEndpoint(contract, new WSHttpBinding(SecurityMode.None), "/wsHttp.svc");
            host.AddServiceEndpoint(contract, new WSHttpBinding(SecurityMode.Transport), "/wsHttp.svc");
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
