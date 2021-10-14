using System;
using StandardClient;
using System.Threading.Tasks;

namespace NetCoreClient
{
    internal static class Program
    {
        /// <summary>
        /// use commanline argument localhost or something similar to indicate the WCF Server hostname
        /// </summary>
        private static async Task Main(string[] args)
        {
            static void log(string value) => Console.WriteLine(value);

            Console.Title = "WCF .Net Core Client";
            string hostname = StartClient(args);

            EchoClientLogic.BuildClientSettings(hostname);
            await EchoClientLogic.InvokeUsingWcf(log);
            string rawSoapResponse = EchoClientLogic.InvokeWebRequest();
            Console.WriteLine($"Http SOAP Response:\n{rawSoapResponse}");

            Console.WriteLine("Hit enter to exit");
            Console.ReadLine();
        }

        private static string StartClient(string[] args)
        {
            string hostname = args.Length >= 1 ? args[0] : null;
            const string s_hostname = "localhost";
            string title = Console.Title;
            if (string.IsNullOrWhiteSpace(hostname)) hostname = s_hostname;
            Console.WriteLine(title + " - " + hostname);
            return hostname;
        }
    }
}
