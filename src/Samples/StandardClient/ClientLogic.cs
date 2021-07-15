﻿using System;
using System.ServiceModel;
using System.ServiceModel.Description;
using System.Text;
using Contract;
using CoreWCF.Samples.StandardCommon;

namespace StandardClient
{
    public class ClientLogic
    {

        public static void CallUsingWcf(
            Settings settings,
            Action<string> log)
        {
            var echo = (Func<IEchoService, string>)(channel =>
               channel.Echo("Hello"));
            var echoFault = (Func<IEchoService, bool>)(channel =>
            {
                try
                {
                    channel.FailEcho("Hello Fault");
                }
                catch (FaultException<EchoFault> e)
                {
                    Console.WriteLine("FaultException<EchoFault>: fault with " + e.Detail.Text);
                    ((IClientChannel)channel).Abort();
                }
                return false;
            });

            log($"BasicHttp:\n\tEcho(\"Hello\") => "
                + echo.WcfInvoke(new BasicHttpBinding(BasicHttpSecurityMode.None), settings.basicHttpAddress));

            log($"BasicHttp:\nFailEcho(\"Hello Fault\") => "
                + echoFault.WcfInvoke(new BasicHttpBinding(BasicHttpSecurityMode.None), settings.basicHttpAddress));

            log($"WsHttp:\n\tEcho(\"Hello\") => "
                + echo.WcfInvoke(new WSHttpBinding(SecurityMode.None), settings.wsHttpAddress));

            log($"NetHttp:\n\tEcho(\"Hello\") => "
                + echo.WcfInvoke(new NetTcpBinding(), settings.netTcpAddress));

            void RunExampleWsHttpsTransportWithMessageCredential ()
            {
                WSHttpBinding binding = new WSHttpBinding(SecurityMode.TransportWithMessageCredential);
                binding.ApplyDebugTimeouts();
                binding.Security.Message.ClientCredentialType = MessageCredentialType.UserName;
                log($"WsHttps TransportWithMessageCredential:\n\tEcho(\"Hello\") => "
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
                log($"BasicHttps:\n\tEcho(\"Hello\") => "
                    + echo.WcfInvoke(new BasicHttpsBinding(BasicHttpsSecurityMode.Transport), settings.basicHttpsAddress));

                log($"WsHttps:\n\tEcho(\"Hello\") => "
                    + echo.WcfInvoke(new WSHttpBinding(SecurityMode.Transport), settings.wsHttpsAddress));

                RunExampleWsHttpsTransportWithMessageCredential();
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
        public static string CallUsingWebRequest(Uri address)
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
