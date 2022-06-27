using System;
//using StandardClient;
//using CoreWCF.Samples.StandardCommon;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
using Contract;

namespace NetCoreClient
{
    public class Program
    {
        /// <remarks>
        /// use commanline argument "http://localhost" BasicHttpBinding
        /// or something similar to indicate the WCF Server hostname
        /// </remarks>
        static void Main(string[] args)
        {
            string hostAddr = args.Length >= 1 ? args[0] : "http://localhost:8088";

            Console.Title = "WCF .Net Core Client";
            PrintScenarioList();

            while (true)
            {
                Console.WriteLine("Type scenario number, ? for scenario list or Hit enter to exit");
                string answer = Console.ReadLine();

                switch (answer)
                {
                    case "1":
                        CallBasicHttpBinding(hostAddr);
                        break;

                    case "2":
                        CallBasicHttpBinding_Fail(hostAddr);
                        break;

                    case "3":
                        CallWsHttpBinding(hostAddr);
                        break;

                    case "4":
                        CallNetTcpBinding(hostAddr);
                        break;

                    case "?":
                        PrintScenarioList();
                        break;

                    case "": return;
                }
            }


            //Settings settings = ClientLogic.BuildClientSettings(hostname);

            //static void log(string value) => Console.WriteLine(value);
            //ClientLogic.InvokeEchoServiceUsingWcf(settings, log);

            //string rawSoapResponse = ClientLogic.InvokeEchoServiceUsingWebRequest(settings.basicHttpAddress);
            //Console.WriteLine($"Http SOAP Response:\n{rawSoapResponse}");

        }

        private static void CallBasicHttpBinding(string hostAddr)
        {
            IClientChannel channel = null;

            var binding = new BasicHttpBinding(IsHttps(hostAddr) ? BasicHttpSecurityMode.Transport : BasicHttpSecurityMode.None);
            // binding.ApplyDebugTimeouts();

            var factory = new ChannelFactory<IEchoService>(binding, new EndpointAddress($"{hostAddr}/EchoService/basicHttp"));
            factory.Open();
            try
            {
                IEchoService client = factory.CreateChannel();
                channel = client as IClientChannel;

                var result = client.Echo("Hello World!");
                channel.Close();
                Console.WriteLine(result);
            }
            finally
            {
                factory.Close();
            }
        }

        private static void CallBasicHttpBinding_Fail(string hostAddr)
        {
            IClientChannel channel = null;

            var binding = new BasicHttpBinding(BasicHttpSecurityMode.None);
            // binding.ApplyDebugTimeouts();

            var factory = new ChannelFactory<IEchoService>(binding, new EndpointAddress($"{hostAddr}/EchoService/basicHttp"));
            factory.Open();
            try
            {
                IEchoService client = factory.CreateChannel();
                channel = client as IClientChannel;

                var result = client.FailEcho("Hello Fault!");
                channel.Close();
            }
            catch (FaultException e)
            {
                //Todo: I think this should be a FaultEception<T>, but I am just getting a fault Exception
                Console.WriteLine($"Call failed with Exception : {e.Message}");
                channel?.Abort();
            }
            finally
            {
                factory.Close();
            }
        }

        private static void CallWsHttpBinding(string hostAddr)
        {
            IClientChannel channel = null;

            var binding = new WSHttpBinding(IsHttps(hostAddr) ? SecurityMode.Transport : SecurityMode.None);
            // binding.ApplyDebugTimeouts();

            var factory = new ChannelFactory<IEchoService>(binding, new EndpointAddress($"{hostAddr}/EchoService/wsHttp"));
            factory.Open();
            try
            {
                IEchoService client = factory.CreateChannel();
                channel = client as IClientChannel;

                var result = client.Echo("Hello World!");
                channel.Close();
                Console.WriteLine(result);
            }
            finally
            {
                factory.Close();
            }
        }

        private static void CallNetTcpBinding(string hostAddr)
        {
            if (!hostAddr.ToLower().StartsWith("net.tcp://"))
            {
                Console.WriteLine(@"Error: To use NetTCP, the server address needs to be of the form ""net.tcp://server:port""");
                return;
            }
            IClientChannel channel = null;

            var binding = new NetTcpBinding();
            // binding.ApplyDebugTimeouts();

            var factory = new ChannelFactory<IEchoService>(binding, new EndpointAddress($"{hostAddr}/netTcp"));
            factory.Open();
            try
            {
                IEchoService client = factory.CreateChannel();
                channel = client as IClientChannel;

                var result = client.Echo("Hello World!");
                channel.Close();
                Console.WriteLine(result);
            }
            finally
            {
                factory.Close();
            }
        }

        private static bool IsHttps(string url)
        {
            return url.ToLower().StartsWith("https://");
        }

        private static void PrintScenarioList()
        {
            string output = @"
Scenarios:
----------
1. BasicHttpBinding, Call Echo(""Hello World"")
2. BasicHttpBinding, Call EchoFault which throws and error
3. WSHttpBinding, Call Echo(""Hello World"")
4. NetTcpBinding, Call Echo(""Hello World"")
";

            Console.WriteLine(output);
        }

    }
}
