// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ServiceModel;
using System.ServiceModel.Channels;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace BasicHttp
{
    public class ActionMismatchTests
    {
        private readonly ITestOutputHelper _output;

        public ActionMismatchTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void BasicHttpWithMismatchedWsAddressingActionHeader()
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Soap11WSAddressing10Startup>(_output).Build();
            using (host)
            {
                host.Start();
                BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new ChannelFactory<ClientContract.IEchoService>(httpBinding,
                    new EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/BasicWcfService/basichttp.svc")));
                ClientContract.IEchoService channel = factory.CreateChannel();

                // The client uses BasicHttpBinding which sends the action as an HTTP SOAPAction
                // header (e.g. "http://tempuri.org/IEchoService/EchoString").
                // We also add an Action header as a WS-Addressing SOAP header using a custom
                // MessageHeader instance, but with a DIFFERENT value. The server uses
                // Soap11WSAddressing10 so ProcessHttpAddressing compares the HTTP SOAPAction
                // with the SOAP wsa:Action header and detects a mismatch. This causes
                // ParseIncomingMessageAsync to return both a non-null message and a non-null
                // exception (ActionMismatchAddressingException).
                //
                // This test verifies the server properly processes the ActionMismatchAddressingException
                // through the ChannelDispatcher and sends a SOAP fault. The client receives a
                // FaultException with a descriptive action mismatch message.
                try
                {
                    using (var scope = new OperationContextScope((IContextChannel)channel))
                    {
                        var actionHeader = MessageHeader.CreateHeader(
                            "Action",
                            "http://www.w3.org/2005/08/addressing",
                            "http://tempuri.org/IEchoService/DifferentAction");
                        OperationContext.Current.OutgoingMessageHeaders.Add(actionHeader);

                        channel.EchoString("test");
                    }
                    Assert.Fail("Expected a FaultException to be thrown");
                }
                catch (FaultException fe)
                {
                    Assert.Contains("does not match the HTTP SOAP Action", fe.Message);
                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((System.ServiceModel.Channels.IChannel)channel, factory);
                }
            }
        }

        internal class Soap11WSAddressing10Startup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app)
            {
                var textEncoding = new CoreWCF.Channels.TextMessageEncodingBindingElement(
                    CoreWCF.Channels.MessageVersion.Soap11WSAddressing10,
                    System.Text.Encoding.UTF8);
                var httpTransport = new CoreWCF.Channels.HttpTransportBindingElement();
                var customBinding = new CoreWCF.Channels.CustomBinding(textEncoding, httpTransport);

                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.EchoService>();
                    builder.AddServiceEndpoint<Services.EchoService, ServiceContract.IEchoService>(
                        customBinding, "/BasicWcfService/basichttp.svc");
                });
            }
        }
    }
}
