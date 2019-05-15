using System;
using System.ServiceModel;

namespace DesktopClient
{
    class Program
    {
        static void Main(string[] args)
        {
            var factory = new ChannelFactory<Contract.IEchoService>(new NetTcpBinding(), new EndpointAddress("net.tcp://localhost:8808/nettcp"));
            factory.Open();
            var channel = factory.CreateChannel();
            ((IClientChannel)channel).Open();
            Console.WriteLine("net.tcp Echo(\"Hello\") => " + channel.Echo("Hello"));
            ((IClientChannel)channel).Close();
            factory.Close();

            factory = new ChannelFactory<Contract.IEchoService>(new BasicHttpBinding(), new EndpointAddress("http://localhost:8080/basichttp"));
            factory.Open();
            channel = factory.CreateChannel();
            ((IClientChannel)channel).Open();
            Console.WriteLine("http Echo(\"Hello\") => " + channel.Echo("Hello"));
            ((IClientChannel)channel).Close();
            factory.Close();

            Console.WriteLine("Hit enter to exit");
            Console.ReadLine();
        }
    }
}
