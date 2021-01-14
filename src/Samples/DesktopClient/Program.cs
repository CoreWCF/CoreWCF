using System;
using System.ServiceModel;
using System.Text;

namespace DesktopClient
{
    class Program
    {
        private readonly static string _basicHttpEndPointAddress = @"http://localhost:8080/basichttp";
        private readonly static string _soapEnvelopeContent = "<soapenv:Envelope xmlns:soapenv=\"http://schemas.xmlsoap.org/soap/envelope/\"><soapenv:Body><Echo xmlns = \"http://tempuri.org/\" ><text>Hello</text></Echo></soapenv:Body></soapenv:Envelope>";

        static void Main(string[] args)
        {
            var factory = new ChannelFactory<Contract.IEchoService>(new NetTcpBinding(), new EndpointAddress("net.tcp://localhost:8808/nettcp"));
            factory.Open();
            var channel = factory.CreateChannel();
            ((IClientChannel)channel).Open();
            Console.WriteLine("net.tcp Echo(\"Hello\") => " + channel.Echo("Hello"));
            ((IClientChannel)channel).Close();
            factory.Close();

            factory = new ChannelFactory<Contract.IEchoService>(new BasicHttpBinding(), new EndpointAddress(_basicHttpEndPointAddress));
            factory.Open();
            channel = factory.CreateChannel();
            ((IClientChannel)channel).Open();
            Console.WriteLine("http Echo(\"Hello\") => " + channel.Echo("Hello"));
            ((IClientChannel)channel).Close();
            factory.Close();

            // Complex type testing
            factory = new ChannelFactory<Contract.IEchoService>(new BasicHttpBinding(), new EndpointAddress(_basicHttpEndPointAddress));
            factory.Open();
            channel = factory.CreateChannel();
            ((IClientChannel)channel).Open();
            Console.WriteLine("http EchoMessage(\"Complex Hello\") => " + channel.ComplexEcho(new Contract.EchoMessage() { Text = "Complex Hello" }));
            ((IClientChannel)channel).Close();
            factory.Close();

            // 
            // The following sample, creates a basic web request to the specified endpoint, sends the SOAP request and reads the response
            // 

            // Prepare the raw content
            var utf8Encoder = new UTF8Encoding();
            var bodyContentBytes = utf8Encoder.GetBytes(_soapEnvelopeContent);

            // Create the web request
            var webRequest = System.Net.WebRequest.Create(new Uri(_basicHttpEndPointAddress));
            webRequest.Headers.Add("SOAPAction", "http://tempuri.org/IEchoService/Echo");

            webRequest.ContentType = "text/xml";
            webRequest.Method = "POST";
            webRequest.ContentLength = bodyContentBytes.Length;

            // Append the content
            var requestContentStream = webRequest.GetRequestStream();
            requestContentStream.Write(bodyContentBytes, 0, bodyContentBytes.Length);

            // Send the request and read the response
            using (System.IO.Stream responseStream = webRequest.GetResponse().GetResponseStream())
            {
                using (System.IO.StreamReader responsereader = new System.IO.StreamReader(responseStream))
                {
                    var soapResponse = responsereader.ReadToEnd();
                    Console.WriteLine($"Http SOAP Response: {soapResponse}");
                }
            }

            Console.WriteLine("Hit enter to exit");
            Console.ReadLine();
        }
    }
}
