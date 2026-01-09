// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.ServiceModel.Channels;
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

        [WindowsOnlyFact]
        public void RemoteEndpointMessageProperty()
        {
            IHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.NetTcpBinding nettcpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IRemoteEndpointMessageProperty>(nettcpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri(host.GetNetTcpAddressInUse() + "/RemoteEndpointMessagePropertyService.svc")));
                ClientContract.IRemoteEndpointMessageProperty channel = factory.CreateChannel();
                try
                {
                    channel.Open();
                    Message request = Message.CreateMessage(nettcpBinding.MessageVersion, "echo", "PASS");
                    Message response = channel.Echo(request);

                    string[] results = response.GetBody<string>().Split(';');
                    Assert.Equal(3, results.Length);
                    Assert.Equal("PASS", results[0]);

                    string clientIP = results[1];
                    CheckIP(clientIP);
                    NetstatResults(_output, results[2], host.GetNetTcpPortInUse().ToString());

                    // *** CLEANUP *** \\
                    channel.Close();
                    factory.Close();
                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((IChannel)channel, factory);
                }
            }
        }

        internal class Startup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.RemoteEndpointMessagePropertyService>();
                    builder.AddServiceEndpoint<Services.RemoteEndpointMessagePropertyService, ServiceContract.IRemoteEndpointMessageProperty>(new NetTcpBinding(SecurityMode.None), "/RemoteEndpointMessagePropertyService.svc");
                });
            }
        }

        private void NetstatResults(ITestOutputHelper output, string clientPort, string endpointPort)
        {
            Process netstatProcess = new Process();
            var netstatFileName = "netstat";
            // netstat moved from /bin/netstat to /usr/sbin/netstat between ubuntu 18.04 and 20.04
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                if (File.Exists("/bin/netstat"))
                {
                    netstatFileName = "/bin/netstat";
                }
                else if (File.Exists("/usr/bin/netstat"))
                {
                    netstatFileName = "/usr/bin/netstat";
                }
                // Fallback to hoping it's on the path
            }
            netstatProcess.StartInfo.FileName = netstatFileName;
            netstatProcess.StartInfo.Arguments = "-a -n";
            netstatProcess.StartInfo.RedirectStandardOutput = true;
            netstatProcess.StartInfo.UseShellExecute = false;

            // get the netstat results while the connection is open
            try
            {
                netstatProcess.Start();
            }
            catch (Win32Exception we)
            {
                throw new Exception($"Tried to execute \"{netstatFileName}\"", we);
            }
            CheckPort(output, clientPort, endpointPort, netstatProcess);
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
        private void CheckPort(ITestOutputHelper output, string clientPort, string servicePort, Process myProcess)
        {
            string line;
            string originPort;
            string destinationPort = null;
            string rawLine = null;
            string matchingPortRawLine = null;
            bool verifiedClientPort = false;
            bool extraLogging = false;
#if NETFRAMEWORK
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                extraLogging = true;
            }
#endif
            if (extraLogging) output.WriteLine("Client port: " + clientPort + ", Service port: " + servicePort + ", Process exited?: " + myProcess.HasExited);
            while (!myProcess.StandardOutput.EndOfStream)
            {
                rawLine = line = myProcess.StandardOutput.ReadLine();
                if (extraLogging) output.WriteLine("Read new line:" + line);
                int index = line.IndexOf("]");

                if (index > -1) //address is IPv6 need to find location of appropriate ':'
                {
                    line = line.Substring(++index);
                    if (extraLogging) output.WriteLine("Origin Address is IPv6, line is now:" + line);
                }

                index = line.IndexOf(":");

                if (index > -1)
                {
                    originPort = GetPort(output, extraLogging, line.Substring(++index));

                    if (originPort == clientPort)
                    {
                        if (extraLogging) output.WriteLine("Origin port matches client port: \"" + originPort + "\" == \"" + clientPort + "\"");
                        verifiedClientPort = true;
                        line = line.Substring(++index);
                        if (extraLogging) output.WriteLine("Line is now:" + line);
                        index = line.IndexOf("]");

                        if (index > -1) //address is IPv6 need to find location of appropriate ':'
                        {
                            line = line.Substring(++index);
                            if (extraLogging) output.WriteLine("Destination Address is IPv6, line is now:" + line);
                        }

                        index = line.IndexOf(":");
                        destinationPort = GetPort(output, extraLogging, line.Substring(++index));
                        matchingPortRawLine = rawLine;

                        if (destinationPort == servicePort)
                        {
                            if (extraLogging) output.WriteLine("Destination port matches service port: \"" + destinationPort + "\" == \"" + servicePort + "\"");
                            return;
                        }
                        if (extraLogging) output.WriteLine("Destination port does not match service port: \"" + destinationPort + "\" != \"" + servicePort + "\"");
                    }
                }
            }

            // Test sometimes fail where the destinationPort string is empty so failing the destingationPort == servicePort check. Added more logging to help debug what's causing this.
            Assert.True(verifiedClientPort, "Reported port does not match client machine info. Client port: \"" + clientPort + "\", Service port (expected): \"" + servicePort + "\", Destination port (actual): \""
                + destinationPort + "\", client port matching line:" + Environment.NewLine + matchingPortRawLine);
            Assert.False(verifiedClientPort, "Reported port did not match any ports used by client.  Reported port: " + clientPort);
        }

        private string GetPort(ITestOutputHelper output, bool extraLogging, string str)
        {
            if (extraLogging) output.WriteLine("GetPort:" + str);
            int index = str.IndexOf(" ");
            string portString = str.Substring(0, index);
            if (extraLogging) output.WriteLine("PortString:*" + portString + "*");
            return portString;
        }
    }
}
