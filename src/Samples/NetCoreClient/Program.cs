using System;
using StandardClient;

namespace NetCoreClient
{
    class Program
    {
        private readonly static string _baseHttpAddress = @"localhost:8088";
        private readonly static string _baseHttpsAddress = @"localhost:8443";
        private readonly static string _baseTcpAddress = @"localhost:8089";

        static void Main(string[] args)
        {
            var settings = new ClientLogic.Settings
            {
                basicHttpAddress = $"http://{_baseHttpAddress}/basichttp",
                basicHttpsAddress = $"https://{_baseHttpsAddress}/basichttp",
                wsHttpAddress = $"http://{_baseHttpAddress}/wsHttp.svc",
                wsHttpsAddress = $"https://{_baseHttpsAddress}/wsHttp.svc",
                netTcpAddress = $"net.tcp://{_baseTcpAddress }/nettcp"
            };

            void log(string value) => Console.WriteLine(value);
            ClientLogic.CallUsingWcf(settings, log);

            string rawSoapResponse = ClientLogic.CallUsingWebRequest(settings.basicHttpAddress);
            Console.WriteLine($"Http SOAP Response:\n{rawSoapResponse}");

            Console.WriteLine("Hit enter to exit");
            Console.ReadLine();
        }

    }
}
