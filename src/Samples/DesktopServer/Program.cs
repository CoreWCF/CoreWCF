using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;

namespace DesktopServer
{
    class Program
    {
        static void Main()
        {
            string hostname = "localhost"; //"localtest"

            IEnumerable<string> baseAddressList = new[] {
                "net.tcp://localhost:8089/",
                "http://localhost:8088/",
                "https://localhost:8443/" };
            baseAddressList = baseAddressList.Select(i => i.Replace("//localhost:", $"//{hostname}:"));
            var baseUriList = baseAddressList.Select(a => new Uri(a)).ToArray();

            Type contract = typeof(Contract.IEchoService);
            var host = new ServiceHost(typeof(EchoService), baseUriList);

            host.AddServiceEndpoint(contract, new BasicHttpBinding(BasicHttpSecurityMode.None), "/basichttp");
            host.AddServiceEndpoint(contract, new BasicHttpsBinding(BasicHttpsSecurityMode.Transport), "/basichttp");
            host.AddServiceEndpoint(contract, new WSHttpBinding(SecurityMode.None), "/wsHttp.svc");
            host.AddServiceEndpoint(contract, new WSHttpBinding(SecurityMode.Transport), "/wsHttp.svc");

            var serverBindingHttpsUserPassword = new WSHttpBinding(SecurityMode.TransportWithMessageCredential);
            serverBindingHttpsUserPassword.Security.Message.ClientCredentialType = MessageCredentialType.UserName;
            host.AddServiceEndpoint(contract, serverBindingHttpsUserPassword, "/wsHttpUserPassword.svc");
            CustomUserNamePasswordValidator.AddToHost(host);

            host.AddServiceEndpoint(contract, new NetTcpBinding(), "/nettcp");
            host.Open();
            foreach(System.ServiceModel.Description.ServiceEndpoint endpoint in host.Description.Endpoints)
            {
                Console.WriteLine("Listening on " + endpoint.ListenUri.ToString());
            }
            Console.WriteLine("Hit enter to close");
            Console.ReadLine();
            host.Close();
        }
    }
}
