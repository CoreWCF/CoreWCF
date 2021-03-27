using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel;
using System.Text;
using System.Threading.Tasks;

namespace DesktopServer
{
    class Program
    {
        static void Main(string[] args)
        {
            var host = new ServiceHost(typeof(EchoService),
                new Uri("net.tcp://localhost:8808/"),
                new Uri("https://localhost:8080/"));
            host.AddServiceEndpoint(typeof(Contract.IEchoService), new NetTcpBinding(), "/nettcp");
            host.AddServiceEndpoint(typeof(Contract.IEchoService), new BasicHttpBinding(BasicHttpSecurityMode.TransportWithMessageCredential), "/basichttp");
            host.Open();
            foreach (var endpoint in host.Description.Endpoints)
            {
                Console.WriteLine("Listening on " + endpoint.ListenUri.ToString());
            }
            Console.WriteLine("Hit enter to close");
            Console.ReadLine();
            host.Close();
        }
    }
}
