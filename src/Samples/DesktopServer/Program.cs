using System;
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
    }
}
