// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using CoreWCF.Channels;
using Helpers;
using Microsoft.AspNetCore.Hosting;
using Xunit;
using Xunit.Abstractions;
using static WSHttp.SimpleWSHTTPTest;

namespace CoreWCF.Http.Tests
{
    public class Issue1003Test
    {
        private readonly ITestOutputHelper _outputHelper;

        public Issue1003Test(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
        }

        private static async Task<string> RequestAndAssert(string url, string text, string action)
        {
            using (HttpClient client = new HttpClient())
            using (HttpContent content = new StringContent(text, Encoding.UTF8, "application/soap+xml"))
            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Headers.Add("SOAPAction", action);
                request.Headers.TryAddWithoutValidation("Content-Type", "application/soap+xml; charset=utf-8");
                request.Content = content;
                using (HttpResponseMessage response = await client.SendAsync(request))
                {
                    response.EnsureSuccessStatusCode(); // throws an Exception if 404, 500, etc.
                    return await response.Content.ReadAsStringAsync();
                }
            }
        }

        [Fact]
        public async Task WSHttpRequestReplyWithTransportMessageCertificateWhenSignatureElementWithPrefix()
        {
            ServicePointManager.ServerCertificateValidationCallback += new RemoteCertificateValidationCallback(ValidateCertificate);
            IWebHost host = ServiceHelper.CreateHttpsWebHostBuilder<WSHttpTransportWithMessageCredentialWithCertificateNoSecurityContext>(_outputHelper).Build();
            using (host)
            {
                host.Start();

                var content = @"
<soap:Envelope xmlns:soap=""http://www.w3.org/2003/05/soap-envelope"">
  <soap:Header xmlns:wsa=""http://www.w3.org/2005/08/addressing"">
    <wsse:Security xmlns:wsse=""http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd"" xmlns:wsu=""http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd""><wsu:Timestamp wsu:Id=""TS-F221523BABC8C1C6DC169178258963930""><wsu:Created>2023-08-11T19:36:29.638Z</wsu:Created><wsu:Expires>2023-08-11T19:41:29.638Z</wsu:Expires></wsu:Timestamp><wsse:BinarySecurityToken EncodingType=""http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-soap-message-security-1.0#Base64Binary"" ValueType=""http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-x509-token-profile-1.0#X509v3"" wsu:Id=""X509-F221523BABC8C1C6DC169178258962925"">MIIFRzCCAy+gAwIBAgIKeJqP3QAAAAACETANBgkqhkiG9w0BAQsFADAwMRMwEQYKCZImiZPyLGQBGRYDYWJjMRkwFwYDVQQDExBhYmMtVzIwMDhFVkFMLUNBMB4XDTIyMDgzMTEwMjczMFoXDTI3MDgzMTEwMzczMFowYzELMAkGA1UEBhMCTFYxDTALBgNVBAgTBFJpZ2ExDTALBgNVBAcTBFJpZ2ExFTATBgNVBAoTDEFCQyBzb2Z0d2FyZTELMAkGA1UECxMCSVQxEjAQBgNVBAMTCW9zYi10ZXN0MzCCASIwDQYJKoZIhvcNAQEBBQADggEPADCCAQoCggEBAKg4mBpxoNRt7ZI6Aayx4SsmFByFQQlBgDHRTflnJ2BfFllfjehRIT8W5qjLMlGSX8bTLzgp4/AX9usYJJAJRqaXefTyM13w8duh2mAkASZh0ta/2RI1Ax4faDhl2C3G958joUfNM0zQcE5EwzSxfWheanifJ+rbKIiyzgb47DViuvMh0CV3gWWF1maHYg/2x0jpPAcFGAMBxMg9XKuvIanatVgzUgbiiXhUuRv25ar+X2yeqptt47HRQeQV+Fjte0oJ8+WJYL2RC+WVHWM3+7668rgM0vlYc56r+NRqKeqzz56KYYnX/Ho5jLXh1nqG/MhfexPZlZYR1Y4AOfkyWpkCAwEAAaOCAS4wggEqMA4GA1UdDwEB/wQEAwIEMDATBgNVHSUEDDAKBggrBgEFBQcDAjAdBgNVHQ4EFgQUCauj0NE/H6zEcfXK8e8fa5ORzMkwHwYDVR0jBBgwFoAU8k8UyVtX4VhJK6sv0VVcwftl1TIwTQYDVR0fBEYwRDBCoECgPoY8aHR0cDovL2FiY3Nzby5hYmNzb2Z0d2FyZS5sdi9DZXJ0RW5yb2xsL2FiYy1XMjAwOEVWQUwtQ0EuY3JsMGYGCCsGAQUFBwEBBFowWDBWBggrBgEFBQcwAoZKaHR0cDovL2FiY3Nzby5hYmNzb2Z0d2FyZS5sdi9DZXJ0RW5yb2xsL1cyMDA4RXZhbC5hYmNfYWJjLVcyMDA4RVZBTC1DQS5jcnQwDAYDVR0TAQH/BAIwADANBgkqhkiG9w0BAQsFAAOCAgEATxhNH80lDXRC51hqa3srDNsK/+ixVeeTug0ckGkaSYMWPvRLl0sJ2I8Wj5XnM/rD8e+ojN3+lm/6FPZdcSc5dZiLuAFyN2b2CTxnVNQvFQaXiXuhTCC7tIu+G39pOmSZ9/2qx+lDv2liVXyLJ9JTFEInMj5ZLiy5FEQi/JeUM3OWVZ4ePoHT2lqHX9HX0i0Az34ehtsFM2ahvGaJ1hK4DemV6Xh9J9t9rTfiTxqCuXc3dGkUjZgHMbA1eKq2LbIw1KJX5jU8oZFkxzmwEWUI65im+wFiXhhzeBAFTOPBKQ81iIEHAU5BSSpgPLUEa8WgiKLkUqQNy9YI4/BiA9e/CB492W3Yz5NCfTiHf3JcbLrD6qT4F1RiSsGp1BFhO5sgnytScN5Xt/lz3B7mFmCeRZTw55Llm32XHPecX8YSPnSBVSqxjBOkLH/3F1vT+jW1uWxa9LKLrRPUlQhoZCIA36VU2doY0cNa/CUqcV+KZQ5dPRdDtf4ZWB+L4c40TW5EOs8YjilyIuxWX85Y9UYDiePwN4qtS4ttZADB2MDB0ACFtvjaGWJFyhySg//y4ueiCu94pDq4207RxWaU7mzS4eW6tmnKZzdBH5Sjc0Fhe7RHL6aSb5HvWdXiawaJclDUq1e8GkaWBR47lWQIVbIxrESU2n43hBhDbdEQ8qUwPNQ=</wsse:BinarySecurityToken><ds:Signature Id=""SIG-F221523BABC8C1C6DC169178258963229"" xmlns:ds=""http://www.w3.org/2000/09/xmldsig#""><ds:SignedInfo><ds:CanonicalizationMethod Algorithm=""http://www.w3.org/2001/10/xml-exc-c14n#""><ec:InclusiveNamespaces PrefixList=""wsa soap"" xmlns:ec=""http://www.w3.org/2001/10/xml-exc-c14n#""/></ds:CanonicalizationMethod><ds:SignatureMethod Algorithm=""http://www.w3.org/2000/09/xmldsig#rsa-sha1""/><ds:Reference URI=""#id-F221523BABC8C1C6DC169178258962928""><ds:Transforms><ds:Transform Algorithm=""http://www.w3.org/2001/10/xml-exc-c14n#""><ec:InclusiveNamespaces PrefixList="""" xmlns:ec=""http://www.w3.org/2001/10/xml-exc-c14n#""/></ds:Transform></ds:Transforms><ds:DigestMethod Algorithm=""http://www.w3.org/2000/09/xmldsig#sha1""/><ds:DigestValue>VCU5evmwiC+opP1vt9wqHnNAyFY=</ds:DigestValue></ds:Reference></ds:SignedInfo><ds:SignatureValue>W4KluFK95uyaMfjlynrh2+FXU8Cq/eVJPkeWVHIojESRsUc12gMm/ZJm73o7Lb9sRmEwYq68QKCB8k12uZ0JaR5tNl4M2zQx+EHM05MHVHD5ymPWvGxHdwzrqQNPE2CuEeCOzaMF/IjOcOC0LJxgAfqhc5eO1VDXFc1+8I+5RI9ZqNmD10tMGzf753D+cyaT9kazoh9TcW43jLMavWji9Y+NyUf07zZeJPibSltzWi7P0V/lQmcwrySzczWqaAVlkCBeuHfM1011++faHD6QOQclvEG4bV8B/7c9cSOCC+ri4INArcyXWtgYntuV2Us/9yEivR+VNpTU0T/QyFGpIA==</ds:SignatureValue><ds:KeyInfo Id=""KI-F221523BABC8C1C6DC169178258962926""><wsse:SecurityTokenReference wsu:Id=""STR-F221523BABC8C1C6DC169178258962927""><wsse:Reference URI=""#X509-F221523BABC8C1C6DC169178258962925"" ValueType=""http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-x509-token-profile-1.0#X509v3""/></wsse:SecurityTokenReference></ds:KeyInfo></ds:Signature></wsse:Security>
    <wsa:Action soap:mustUnderstand=""1"">http://tempuri.org/IEchoService/EchoString</wsa:Action>
    <wsa:ReplyTo soap:mustUnderstand=""1""><wsa:Address>http://www.w3.org/2005/08/addressing/anonymous</wsa:Address></wsa:ReplyTo>
    <wsa:To soap:mustUnderstand=""1"" wsu:Id=""id-F221523BABC8C1C6DC169178258962928"" xmlns:wsu=""http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-utility-1.0.xsd"">http://tempuri.org/IEchoService/EchoString</wsa:To>
   </soap:Header>
   <soap:Body>
    <EchoString xmlns=""http://tempuri.org/"">
      <echo>aaaa</echo>
    </EchoString>
   </soap:Body>
</soap:Envelope>";

                string requestUri = $"https://localhost:{host.GetHttpsPort()}/WSHttpWcfService/basichttp.svc";
                string action = "http://tempuri.org/IEchoService/EchoString";
                await RequestAndAssert(requestUri, content, action);

                Console.WriteLine("read ");
            }
        }

        private static bool ValidateCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors errors)
        {
            return true;
        }

        internal class WSHttpTransportWithMessageCredentialWithCertificateNoSecurityContext : WSHttpTransportWithMessageCredentialWithCertificate
        {
            public override Binding ChangeBinding(WSHttpBinding wsBInding)
            {
                wsBInding.Security.Message.EstablishSecurityContext = false;
                CustomBinding myCustomBinding = new CustomBinding(wsBInding);
                TransportSecurityBindingElement security = myCustomBinding.Elements.Find<TransportSecurityBindingElement>();
                security.LocalServiceSettings.MaxClockSkew = TimeSpan.MaxValue;

                return myCustomBinding;
            }
        }
    }
}
