// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Net;
using System.ServiceModel.Channels;
using System.Threading;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.NetTcp.Tests
{
    public class TransportFrameworkTests
    {
        private readonly ITestOutputHelper _output;

        public TransportFrameworkTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void RemoteEndpointMessageProperty()
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.NetTcpBinding nettcpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IRemoteEndpointMessageProperty>(nettcpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri(host.GetNetTcpAddressInUse() + "/RemoteEndpointMessagePropertyService.svc")));
                ClientContract.IRemoteEndpointMessageProperty channel = factory.CreateChannel();

                Message request = Message.CreateMessage(nettcpBinding.MessageVersion, "echo", "PASS");
                Message response = channel.Echo(request);

                string[] results = response.GetBody<string>().Split(';');
                Assert.Equal(3, results.Length);
                Assert.Equal("PASS", results[0]);

                string clientIP = results[1];
                CheckIP(clientIP);
                NetstatResults(results[2], host.GetNetTcpPortInUse().ToString());
            }
        }

        internal class Startup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app, IHostingEnvironment env)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.RemoteEndpointMessagePropertyService>();
                    builder.AddServiceEndpoint<Services.RemoteEndpointMessagePropertyService, ServiceContract.IRemoteEndpointMessageProperty>(new NetTcpBinding(SecurityMode.None), "/RemoteEndpointMessagePropertyService.svc");
                });
            }
        }

        private void NetstatResults(string clientPort, string endpointPort)
        {
            Process netstatProcess = new Process();
            netstatProcess.StartInfo.FileName = "netstat";
            netstatProcess.StartInfo.Arguments = "-a -n";
            netstatProcess.StartInfo.RedirectStandardOutput = true;
            netstatProcess.StartInfo.UseShellExecute = false;

            // get the netstat results while the connection is open
            netstatProcess.Start();
            CheckPort(clientPort, endpointPort, netstatProcess);
        }

        private void CheckIP(string ip)
        {
            bool addressMatches = false;
            IPAddress[] addresses = Dns.GetHostAddresses("localhost");
            foreach (IPAddress address in addresses)
            {
                if (address.ToString() == ip)
                {
                    addressMatches = true;
                    break;
                }
            }

            Assert.True(addressMatches);
        }

        // netstate -a shows all current TCP and UDP connections
        // function will examine the origin port of all connections to find match with port report
        // to service from the RemoteEndpointMessageProperty
        // This port should be connected to the port used by the service
        // Terms used in this function:
        // clientPort:  port obtained from RemoteEndpointMessageProperty at service
        // originPort:  port listed as the clients outgoing port by netstat -a
        // destinationPort: port that originPort connected to
        // servicePOrt: port that the service is actually listening at
        //
        // A succesful pass will include the client and origin matching and the
        // service and destination matching
        private void CheckPort(string clientPort, string servicePort, Process myProcess)
        {
            string line;
            string originPort;
            string destinationPort = null;
            bool verifiedClientPort = false;

            while (!myProcess.StandardOutput.EndOfStream)
            {
                line = myProcess.StandardOutput.ReadLine();

                int index = line.IndexOf("]");

                if (index > -1) //address is IPv6 need to find location of appropriate ':'
                {
                    line = line.Substring(++index);
                }

                index = line.IndexOf(":");

                if (index > -1)
                {
                    originPort = GetPort(line.Substring(++index));

                    if (originPort == clientPort)
                    {
                        verifiedClientPort = true;
                        line = line.Substring(++index);

                        index = line.IndexOf("]");

                        if (index > -1) //address is IPv6 need to find location of appropriate ':'
                        {
                            line = line.Substring(++index);
                        }

                        index = line.IndexOf(":");
                        destinationPort = GetPort(line.Substring(++index));

                        if (destinationPort == servicePort)
                        {
                            return;
                        }
                    }
                }
            }

            Assert.True(verifiedClientPort, "Reported port does not match client machine info. Client port: " + clientPort + ", Service port (expected): " + servicePort + ", Destination port (actual): " + destinationPort);
            Assert.False(verifiedClientPort, "Reported port did not match any ports used by client.  Reported port: " + clientPort);
        }

        private string GetPort(string str)
        {
            int index = str.IndexOf(" ");
            return str.Substring(0, index);
        }
    }
}
