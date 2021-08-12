using System;
using StandardClient;
using CoreWCF.Samples.StandardCommon;

namespace DesktopClient
{
    class Program
    {
        private static readonly string s_hostname = "localhost";

        static void Main()
        {
            Settings settings = new Settings().SetDefaults(s_hostname);

            void log(string value) => Console.WriteLine(value);
            ClientLogic.CallUsingWcf(settings, log);

            string rawSoapResponse = ClientLogic.CallUsingWebRequest(settings.basicHttpAddress);
            Console.WriteLine($"Http SOAP Response:\n{rawSoapResponse}");

            Console.WriteLine("Hit enter to exit");
            Console.ReadLine();
        }

    }
}
