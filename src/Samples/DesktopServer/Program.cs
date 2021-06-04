using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using CoreWCF.Samples.StandardCommon;

namespace DesktopServer
{
    class Program
    {
        static void Main()
        {
            Settings settings = new Settings().SetDetaults();

            string hostname = "localhost"; //"localtest"

            IEnumerable<string> baseAddressList = new[] {
                "net.tcp://localhost:8089/",
                "http://localhost:8088/",
                "https://localhost:8443/" };
            baseAddressList = baseAddressList.Select(i => i.Replace("//localhost:", $"//{hostname}:"));
            Uri[] baseUriList = baseAddressList.Select(a => new Uri(a)).ToArray();

            Type contract = typeof(Contract.IEchoService);
            var host = new ServiceHost(typeof(EchoService), baseUriList);

            host.AddServiceEndpoint(contract, new BasicHttpBinding(BasicHttpSecurityMode.None), settings.basicHttpAddress.LocalPath);
            host.AddServiceEndpoint(contract, new BasicHttpsBinding(BasicHttpsSecurityMode.Transport), settings.basicHttpsAddress.LocalPath);
            host.AddServiceEndpoint(contract, new WSHttpBinding(SecurityMode.None), settings.wsHttpAddress.LocalPath);
            host.AddServiceEndpoint(contract, new WSHttpBinding(SecurityMode.Transport), settings.wsHttpsAddress.LocalPath);

            var serverBindingHttpsUserPassword = new WSHttpBinding(SecurityMode.TransportWithMessageCredential);
            serverBindingHttpsUserPassword.Security.Message.ClientCredentialType = MessageCredentialType.UserName;
            host.AddServiceEndpoint(contract, serverBindingHttpsUserPassword, settings.wsHttpAddressValidateUserPassword.LocalPath);
            CustomUserNamePasswordValidator.AddToHost(host);

            host.AddServiceEndpoint(contract, new NetTcpBinding(), settings.netTcpAddress.LocalPath);
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
