// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CoreWCF.Http.Tests
{
    // MaxReceivedMessageSize is an inclusive limit: a body of exactly that many bytes is accepted and
    // only a larger one faults, with requests answered by 413 rather than a plain 500.
    public class MaxReceivedMessageSizeTests
    {
        private const int MaxReceivedMessageSize = 4096;
        private const string SoapAction = "http://tempuri.org/IEchoService/EchoString";

        private readonly ITestOutputHelper _output;

        public MaxReceivedMessageSizeTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task BodyExactlyAtTheLimitIsAccepted()
        {
            HttpResponseMessage response = await PostEnvelopeAsync(MaxReceivedMessageSize);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        [Fact]
        public async Task BodyPastTheLimitIsRejectedWithRequestEntityTooLarge()
        {
            HttpResponseMessage response = await PostEnvelopeAsync(MaxReceivedMessageSize + 1);

            Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        }

        private async Task<HttpResponseMessage> PostEnvelopeAsync(int envelopeSize)
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder(_output, typeof(Startup)).Build();
            using (host)
            {
                host.Start();

                using var client = new HttpClient();
                var content = new StringContent(BuildEnvelope(envelopeSize), Encoding.UTF8, "text/xml");
                content.Headers.Add("SOAPAction", $"\"{SoapAction}\"");

                return await client.PostAsync(
                    $"http://localhost:{host.GetHttpPort()}/BasicWcfService/basichttp.svc", content);
            }
        }

        private static string BuildEnvelope(int totalBytes)
        {
            const string prefix = @"<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/""><s:Body><EchoString xmlns=""http://tempuri.org/""><echo>";
            const string suffix = @"</echo></EchoString></s:Body></s:Envelope>";

            string envelope = prefix + new string('a', totalBytes - prefix.Length - suffix.Length) + suffix;

            // All ASCII, so the character count is the byte count the server sees.
            Assert.Equal(totalBytes, Encoding.UTF8.GetByteCount(envelope));
            return envelope;
        }

        internal class Startup
        {
            public void ConfigureServices(IServiceCollection services) => services.AddServiceModelServices();

            public void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.EchoService>();
                    var binding = new CustomBinding();
                    binding.Elements.Add(new TextMessageEncodingBindingElement { MessageVersion = MessageVersion.Soap11, ReaderQuotas = XmlDictionaryReaderQuotas.Max });
                    binding.Elements.Add(new HttpTransportBindingElement
                    {
                        MaxReceivedMessageSize = MaxReceivedMessageSize,
                        MaxBufferSize = MaxReceivedMessageSize,
                        TransferMode = TransferMode.Buffered,
                    });
                    builder.AddServiceEndpoint<Services.EchoService, ServiceContract.IEchoService>(binding, "/BasicWcfService/basichttp.svc");
                });
            }
        }
    }
}
