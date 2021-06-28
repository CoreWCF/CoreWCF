using System;
using System.ServiceModel;
using System.Text;
using Contract;

namespace StandardClient
{
    public class ClientLogic
    {
        public class Settings
        {
            public bool UseHttps { get; set; } = true;
            public string basicHttpAddress { get; set; }
            public string basicHttpsAddress { get; set; }
            public string wsHttpAddress { get; set; }
            public string wsHttpsAddress { get; set; }
            public string netTcpAddress { get; set; }
        }

        public static void CallUsingWcf(
            Settings settings,
            Action<string> log)
        {
            var echo = (Func<IEchoService, string>)((IEchoService channel) =>
               channel.Echo("Hello"));

            log($"BasicHttp:\n\tEcho(\"Hello\") => "
                + echo.WcfInvoke(new BasicHttpBinding(BasicHttpSecurityMode.None), settings.basicHttpAddress));

            log($"WsHttp:\n\tEcho(\"Hello\") => "
                + echo.WcfInvoke(new WSHttpBinding(SecurityMode.None), settings.wsHttpAddress));

            log($"NetHttp:\n\tEcho(\"Hello\") => "
                + echo.WcfInvoke(new NetTcpBinding(), settings.netTcpAddress));

            if (settings.UseHttps)
            {
                log($"BasicHttps:\n\tEcho(\"Hello\") => "
                    + echo.WcfInvoke(new BasicHttpsBinding(BasicHttpsSecurityMode.Transport), settings.basicHttpsAddress));

                log($"WsHttps:\n\tEcho(\"Hello\") => "
                    + echo.WcfInvoke(new WSHttpBinding(SecurityMode.Transport), settings.wsHttpsAddress));
            }

            var echoComplex = (Func<IEchoService, string>)((IEchoService channel) =>
               channel.ComplexEcho(new EchoMessage() { Text = "Complex Hello" }));

            log($"BasicHttp with Complex Object:\n\tEcho(\"Hello\") => "
                + echoComplex.WcfInvoke(new NetTcpBinding(), settings.netTcpAddress));
        }

        /// <summary>
        /// Creates a basic web request to the specified endpoint,
        /// sends the SOAP request and reads the response
        /// </summary>
        public static string CallUsingWebRequest(string address)
        {
            string _soapEnvelopeContent =
@"<soapenv:Envelope xmlns:soapenv='http://schemas.xmlsoap.org/soap/envelope/'>
    <soapenv:Body>
    <Echo xmlns='http://tempuri.org/'>
        <text>Hello</text>
    </Echo>
    </soapenv:Body>
</soapenv:Envelope>";

            // Prepare the raw content
            var utf8Encoder = new UTF8Encoding();
            var bodyContentBytes = utf8Encoder.GetBytes(_soapEnvelopeContent);

            // Create the web request
            var webRequest = System.Net.WebRequest.Create(new Uri(address));
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
                    return soapResponse;
                }
            }
        }
    }
}
