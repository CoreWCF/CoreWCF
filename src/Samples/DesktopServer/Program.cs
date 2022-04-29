using System;
using System.Linq;
using System.ServiceModel;

namespace DesktopServer
{
    class Program
    {
        private static ServiceHost ConfigureWcfHost<TService, TContract>(string servicePrefix)
        {
            var httpUrl = "http://localhost:8088";
            var httpsUrl= "https://localhost:8443";
            var netTcpUrl = "net.tcp://localhost:8089";

            Uri[] baseUriList = new Uri[] { new Uri(httpUrl), new Uri(httpsUrl), new Uri(netTcpUrl) };

            Type contract = typeof(TContract);
            var host = new ServiceHost(typeof(TService), baseUriList);

            var serverBindingHttpsUserPassword = new WSHttpBinding(SecurityMode.TransportWithMessageCredential);
            serverBindingHttpsUserPassword.Security.Message.ClientCredentialType = MessageCredentialType.UserName;
            host.AddServiceEndpoint(contract, serverBindingHttpsUserPassword, "/wsHttpUserPassword");
            CustomUserNamePasswordValidator.AddToHost(host);

            host.AddServiceEndpoint(contract, new BasicHttpBinding(BasicHttpSecurityMode.None), "/basichttp");
            host.AddServiceEndpoint(contract, new BasicHttpsBinding(BasicHttpsSecurityMode.Transport), "/basichttp");
            host.AddServiceEndpoint(contract, new WSHttpBinding(SecurityMode.None), "/wsHttp");
            host.AddServiceEndpoint(contract, new WSHttpBinding(SecurityMode.Transport), "/wsHttp");
            host.AddServiceEndpoint(contract, new NetTcpBinding(), "/nettcp");
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
            ServiceHost hostEchoService = ConfigureWcfHost<EchoService, Contract.IEchoService>("EchoService");

            hostEchoService.Open();

            LogHostUrls(hostEchoService);

            Console.WriteLine("Hit enter to close");
            Console.ReadLine();

            hostEchoService.Close();
        }
    }
}
