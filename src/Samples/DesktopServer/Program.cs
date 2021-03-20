using System;
using System.Linq;
using System.Net;
using System.Net.Security;
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
