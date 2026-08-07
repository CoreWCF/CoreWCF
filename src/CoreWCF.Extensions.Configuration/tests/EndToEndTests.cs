// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.ServiceModel.Channels;
using Helpers;
using Microsoft.AspNetCore.Hosting;
using Xunit;

namespace CoreWCF.Extensions.Configuration.Tests
{
    /// <summary>
    /// Stands up a CoreWCF host described only by configuration and calls it with a real WCF client, once per
    /// supported binding. These are what prove the hydrated bindings are usable rather than merely well shaped.
    /// </summary>
    public class EndToEndTests
    {
        private const string ServiceTypeName = "CoreWCF.Extensions.Configuration.Tests.EchoService, CoreWCF.Extensions.Configuration.Tests";
        private const string ContractName = "CoreWCF.Extensions.Configuration.Tests.IEchoService, CoreWCF.Extensions.Configuration.Tests";

        public static TheoryData<string> HttpBindings => new TheoryData<string>
        {
            "basicHttp",
            "netHttp",
            "wsHttp",
            "custom",
        };

        /// <summary>
        /// Every HTTP flavoured binding on one host, each on its own relative address. Kestrel is given port 0, so
        /// the addresses have to be relative and the base address comes from the server.
        /// </summary>
        private static Dictionary<string, string> HttpConfiguration()
        {
            var settings = new Dictionary<string, string>
            {
                ["ServiceModel:Bindings:basicHttp:Type"] = "CoreWCF.BasicHttpBinding, CoreWCF.Http",
                ["ServiceModel:Bindings:basicHttp:MaxReceivedMessageSize"] = "1048576",
                ["ServiceModel:Bindings:basicHttp:TextEncoding"] = "utf-8",

                ["ServiceModel:Bindings:netHttp:Type"] = "CoreWCF.NetHttpBinding, CoreWCF.Http",

                ["ServiceModel:Bindings:wsHttp:Type"] = "CoreWCF.WSHttpBinding, CoreWCF.Http",
                ["ServiceModel:Bindings:wsHttp:Security:Mode"] = "None",

                // The case ConfigurationBinder cannot express: an ordered list of polymorphic binding elements.
                ["ServiceModel:Bindings:custom:Type"] = "CoreWCF.Channels.CustomBinding, CoreWCF.Primitives",
                ["ServiceModel:Bindings:custom:Elements:0:Type"] = "CoreWCF.Channels.TextMessageEncodingBindingElement, CoreWCF.Primitives",
                ["ServiceModel:Bindings:custom:Elements:0:MessageVersion"] = "Soap11",
                ["ServiceModel:Bindings:custom:Elements:0:WriteEncoding"] = "utf-8",
                ["ServiceModel:Bindings:custom:Elements:1:Type"] = "CoreWCF.Channels.HttpTransportBindingElement, CoreWCF.Http",
                ["ServiceModel:Bindings:custom:Elements:1:MaxReceivedMessageSize"] = "1048576",
            };

            int index = 0;
            foreach (string binding in new[] { "basicHttp", "netHttp", "wsHttp", "custom" })
            {
                string prefix = $"ServiceModel:Services:{ServiceTypeName}:Endpoints:{index}";
                settings[$"{prefix}:Contract"] = ContractName;
                settings[$"{prefix}:Binding"] = binding;
                settings[$"{prefix}:Address"] = $"/echo/{binding}.svc";
                index++;
            }

            return settings;
        }

        [Theory]
        [MemberData(nameof(HttpBindings))]
        public void HttpBinding_ConfiguredEndpointAnswersAWcfClient(string binding)
        {
            string expected = new string('a', 512);

            using (IWebHost host = ConfiguredServiceHost.CreateHttpHost(HttpConfiguration()))
            {
                host.Start();

                var address = new System.ServiceModel.EndpointAddress(
                    new Uri($"http://localhost:{host.GetHttpPort()}/echo/{binding}.svc"));

                string actual = Echo(CreateClientBinding(binding), address, expected);

                Assert.Equal(expected, actual);
            }
        }

        [Fact]
        public void NetTcpBinding_ConfiguredEndpointAnswersAWcfClient()
        {
            string expected = new string('b', 512);
            int port = ConfiguredServiceHost.GetAvailableTcpPort();
            string address = $"net.tcp://localhost:{port}/echo/nettcp.svc";

            var settings = new Dictionary<string, string>
            {
                ["ServiceModel:Bindings:internal:Type"] = "CoreWCF.NetTcpBinding, CoreWCF.NetTcp",
                ["ServiceModel:Bindings:internal:Security:Mode"] = "None",
                ["ServiceModel:Bindings:internal:MaxReceivedMessageSize"] = "1048576",
                [$"ServiceModel:Services:{ServiceTypeName}:Endpoints:0:Contract"] = ContractName,
                [$"ServiceModel:Services:{ServiceTypeName}:Endpoints:0:Binding"] = "internal",
                [$"ServiceModel:Services:{ServiceTypeName}:Endpoints:0:Address"] = address,
            };

            using (IWebHost host = ConfiguredServiceHost.CreateNetTcpHost(settings, port))
            {
                host.Start();

                var netTcp = new System.ServiceModel.NetTcpBinding(System.ServiceModel.SecurityMode.None)
                {
                    MaxReceivedMessageSize = 1048576,
                };

                string actual = Echo(netTcp, new System.ServiceModel.EndpointAddress(new Uri(address)), expected);

                Assert.Equal(expected, actual);
            }
        }

        /// <summary>
        /// A single service exposing the same contract over two bindings at once, which is the case a named
        /// binding declaration exists for.
        /// </summary>
        [Fact]
        public void OneService_IsReachableOverSeveralConfiguredBindings()
        {
            string expected = "multi";

            using (IWebHost host = ConfiguredServiceHost.CreateHttpHost(HttpConfiguration()))
            {
                host.Start();
                int port = host.GetHttpPort();

                foreach (string binding in new[] { "basicHttp", "netHttp", "wsHttp", "custom" })
                {
                    var address = new System.ServiceModel.EndpointAddress(
                        new Uri($"http://localhost:{port}/echo/{binding}.svc"));

                    Assert.Equal(expected, Echo(CreateClientBinding(binding), address, expected));
                }
            }
        }

        private static string Echo(
            System.ServiceModel.Channels.Binding binding,
            System.ServiceModel.EndpointAddress address,
            string value)
        {
            var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(binding, address);
            ClientContract.IEchoService channel = null;

            try
            {
                channel = factory.CreateChannel();
                return channel.Echo(value);
            }
            finally
            {
                CloseQuietly((IChannel)channel);
                CloseQuietly(factory);
            }
        }

        private static void CloseQuietly(System.ServiceModel.ICommunicationObject communicationObject)
        {
            if (communicationObject == null)
            {
                return;
            }

            try
            {
                communicationObject.Close();
            }
            catch
            {
                communicationObject.Abort();
            }
        }

        /// <summary>
        /// The client side counterpart of each configured binding, built in code. Hydrating the client's binding
        /// from configuration is the other half of the problem and is not what these tests cover.
        /// </summary>
        private static System.ServiceModel.Channels.Binding CreateClientBinding(string binding)
        {
            switch (binding)
            {
                case "basicHttp":
                    return new System.ServiceModel.BasicHttpBinding { MaxReceivedMessageSize = 1048576 };

                case "netHttp":
                    return new System.ServiceModel.NetHttpBinding();

                case "wsHttp":
                    return new System.ServiceModel.WSHttpBinding(System.ServiceModel.SecurityMode.None);

                case "custom":
                    return new CustomBinding(
                        new TextMessageEncodingBindingElement(MessageVersion.Soap11, System.Text.Encoding.UTF8),
                        new HttpTransportBindingElement { MaxReceivedMessageSize = 1048576 });

                default:
                    throw new ArgumentOutOfRangeException(nameof(binding), binding, "Unknown binding.");
            }
        }
    }
}
