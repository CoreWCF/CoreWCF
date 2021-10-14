using System;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Text;
using System.Threading.Tasks;
using Contract;
using CoreWCF.Samples.StandardCommon;

namespace StandardClient
{
    public class EchoClientLogic
    {
        private static Settings settings;

        public static void BuildClientSettings(string hostname)
        {
            settings = new Settings().SetDefaults(hostname, "EchoService");
        }

        public static async Task InvokeUsingWcf(Action<string> log)
        {
            var echo = (Func<IEchoService, string>)(channel =>
               channel.Echo("Hello"));
            var echoFault = (Func<IEchoService, Task<string>>)(async channel =>
            {
                try
                {
                    channel.FailEcho("Hello Fault");
                    return "No Exception";
                }
                catch (FaultException<EchoFault> ex)
                {
                    return "FaultException<T>: " + ex.Detail.Text;
                }
                catch (FaultException ex)
                {
                    return "FaultException: " + ex.Message;
                }
            });

            log("Echo Operation");

            log("\tBasicHttp FailEcho: => "
                + await echoFault.WcfInvokeAsync(new BasicHttpBinding(BasicHttpSecurityMode.None), settings.basicHttpAddress));

            log("\tBasicHttp: => "
                + echo.WcfInvoke(new BasicHttpBinding(BasicHttpSecurityMode.None), settings.basicHttpAddress));

            log("\tWsHttp: => "
                + echo.WcfInvoke(new WSHttpBinding(SecurityMode.None), settings.wsHttpAddress));

            log("\tWsHttp FailEcho => "
                + echoFault.WcfInvoke(new WSHttpBinding(SecurityMode.None), settings.wsHttpAddress));

            log("\tNetHttp: => "
                + echo.WcfInvoke(new NetTcpBinding(), settings.netTcpAddress));

            void RunExampleWsHttpsTransportWithMessageCredential ()
            {
                WSHttpBinding binding = new WSHttpBinding(SecurityMode.TransportWithMessageCredential);
                binding.ApplyDebugTimeouts();
                binding.Security.Message.ClientCredentialType = MessageCredentialType.UserName;
                log("\tWsHttps TransportWithMessageCredential: => "
                    + echo.WcfInvoke(binding,
                        settings.wsHttpAddressValidateUserPassword,
                        channel => {
                            var clientCredentials = (ClientCredentials)channel.Endpoint.EndpointBehaviors[typeof(ClientCredentials)];
                            clientCredentials.UserName.UserName = "UserName_valid";
                            clientCredentials.UserName.Password = "Password_valid";
                            channel.Credentials.ServiceCertificate.SslCertificateAuthentication = new System.ServiceModel.Security.X509ServiceCertificateAuthentication
                            {
                                CertificateValidationMode = System.ServiceModel.Security.X509CertificateValidationMode.None
                            };
                        }
                        )
                        );
            }

            if (settings.UseHttps)
            {
                log("\tBasicHttps: => "
                    + echo.WcfInvoke(new BasicHttpsBinding(BasicHttpsSecurityMode.Transport), settings.basicHttpsAddress));

                log("\tWsHttps: => "
                    + echo.WcfInvoke(new WSHttpBinding(SecurityMode.Transport), settings.wsHttpsAddress));

                RunExampleWsHttpsTransportWithMessageCredential();
            }

            //var echoComplex = (Func<IEchoService, string>)((IEchoService channel) =>
            //   channel.ComplexEcho(new EchoMessage() { Text = "Complex Hello" }));

            //log("\tBasicHttp with Complex Object: => "
            //    + echoComplex.WcfInvoke(new NetTcpBinding(), settings.netTcpAddress));
        }

        /// <summary>
        /// Creates a basic web request to the specified endpoint,
        /// sends the SOAP request and reads the response
        /// </summary>
        public static string InvokeWebRequest()
        {
            Uri address = settings.basicHttpAddress;

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
            byte[] bodyContentBytes = utf8Encoder.GetBytes(_soapEnvelopeContent);

            // Create the web request
            var webRequest = System.Net.WebRequest.Create(address);
            webRequest.Headers.Add("SOAPAction", "http://tempuri.org/IEchoService/Echo");
            webRequest.ContentType = "text/xml";
            webRequest.Method = "POST";
            webRequest.ContentLength = bodyContentBytes.Length;

            // Append the content
            System.IO.Stream requestContentStream = webRequest.GetRequestStream();
            requestContentStream.Write(bodyContentBytes, 0, bodyContentBytes.Length);

            // Send the request and read the response
            using (System.IO.Stream responseStream = webRequest.GetResponse().GetResponseStream())
            {
                using (System.IO.StreamReader responsereader = new System.IO.StreamReader(responseStream))
                {
                    string soapResponse = responsereader.ReadToEnd();
                    return soapResponse;
                }
            }
        }
    }
}
