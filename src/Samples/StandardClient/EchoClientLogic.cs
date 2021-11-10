using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using System.ServiceModel.Description;
using System.Text;
using System.Threading.Tasks;
using Contract;
using CoreWCF.Samples.StandardCommon;

namespace StandardClient
{
    public static class EchoClientLogic
    {
        private static Settings s_settings;

        public static void BuildClientSettings(string hostname)
        {
            s_settings = new Settings().SetDefaults(hostname, "EchoService");
        }

        public static CustomBinding CreateCustomBinding()
        {
            CustomBinding result = new();
            TextMessageEncodingBindingElement textBindingElement = new()
            {
                //System.ArgumentException : Addressing Version 'AddressingNone (http://schemas.microsoft.com/ws/2005/05/addressing/none)' is not supported. (Parameter 'addressingVersion')
                MessageVersion = MessageVersion.CreateVersion(EnvelopeVersion.Soap12, AddressingVersion.None)
            };
            result.Elements.Add(textBindingElement);
            HttpTransportBindingElement httpBindingElement = new()
            {
                //AllowCookies = true,
                MaxBufferSize = int.MaxValue,
                MaxReceivedMessageSize = int.MaxValue
            };
            result.Elements.Add(httpBindingElement);
            return result;
        }

        public static Task InvokeUsingWcf(Action<string> log)
        {
            var echo = (Func<IEchoService, string>)(channel =>
               channel.Echo("Hello"));
            var echoFault = (Func<IEchoService, string>)(channel =>
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

            var customBinding = CreateCustomBinding();

            log("\tBasicHttp Echo: => "
                + echo.WcfInvoke(customBinding, s_settings.CustomAddress));
            log("\tBasicHttp: => "
                + echo.WcfInvoke(new BasicHttpBinding(BasicHttpSecurityMode.None), s_settings.basicHttpAddress));
            log("\tWsHttp: => "
                + echo.WcfInvoke(new WSHttpBinding(SecurityMode.None), s_settings.wsHttpAddress));
            return Task.CompletedTask;

            log("\tBasicHttp FailEcho: => "
                + echoFault.WcfInvoke(new BasicHttpBinding(BasicHttpSecurityMode.None), s_settings.basicHttpAddress));

            log("\tWsHttp FailEcho => "
                + echoFault.WcfInvoke(new WSHttpBinding(SecurityMode.None), s_settings.wsHttpAddress));

            log("\tNetHttp: => "
                + echo.WcfInvoke(new NetTcpBinding(), s_settings.netTcpAddress));

            void RunExampleWsHttpsTransportWithMessageCredential ()
            {
                WSHttpBinding binding = new WSHttpBinding(SecurityMode.TransportWithMessageCredential);
                binding.ApplyDebugTimeouts();
                binding.Security.Message.ClientCredentialType = MessageCredentialType.UserName;
                log("\tWsHttps TransportWithMessageCredential: => "
                    + echo.WcfInvoke(binding,
                        s_settings.wsHttpAddressValidateUserPassword,
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

            if (s_settings.UseHttps)
            {
                log("\tBasicHttps: => "
                    + echo.WcfInvoke(new BasicHttpsBinding(BasicHttpsSecurityMode.Transport), s_settings.basicHttpsAddress));

                log("\tWsHttps: => "
                    + echo.WcfInvoke(new WSHttpBinding(SecurityMode.Transport), s_settings.wsHttpsAddress));

                RunExampleWsHttpsTransportWithMessageCredential();
            }

            var echoComplex = (Func<IEchoService, string>)((IEchoService channel) =>
               channel.ComplexEcho(new EchoMessage() { Text = "Complex Hello" }));

            log("\tBasicHttp with Complex Object: => "
                + echoComplex.WcfInvoke(new NetTcpBinding(), s_settings.netTcpAddress));

            return Task.CompletedTask;
        }

        /// <summary>
        /// Creates a basic web request to the specified endpoint,
        /// sends the SOAP request and reads the response
        /// </summary>
        public static string InvokeWebRequest()
        {
            Uri address = s_settings.basicHttpAddress;

            const string _soapEnvelopeContent =
@"<soapenv:Envelope xmlns:soapenv='http://schemas.xmlsoap.org/soap/envelope/'>
    <soapenv:Body>
    <Echo xmlns='http://my.service.com'>
        <text>Hello</text>
    </Echo>
    </soapenv:Body>
</soapenv:Envelope>";

            // Prepare the raw content
            var utf8Encoder = new UTF8Encoding();
            byte[] bodyContentBytes = utf8Encoder.GetBytes(_soapEnvelopeContent);

            // Create the web request
            var webRequest = System.Net.WebRequest.Create(address);
            webRequest.Headers.Add("SOAPAction", "http://my.service.com/IEchoService/Echo");
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
