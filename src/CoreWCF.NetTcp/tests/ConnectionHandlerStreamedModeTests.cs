// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ServiceModel.Channels;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace ConnectionHandler
{
    public class ConnectionHandlerStreamedModeTests
    {
        private readonly ITestOutputHelper _output;

        public ConnectionHandlerStreamedModeTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [WindowsOnlyFact]
        public void SimpleNetTcpClientConnectionWindowsAuth()
        {
            string testString = new string('a', 3000);
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                System.ServiceModel.ChannelFactory<ClientContract.ITestService> factory = null;
                ClientContract.ITestService channel = null;
                host.Start();
                try
                {
                    System.ServiceModel.NetTcpBinding binding = ClientHelper.GetStreamedModeBinding(System.ServiceModel.SecurityMode.Transport);
                    factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(binding,
                        new System.ServiceModel.EndpointAddress(host.GetNetTcpAddressInUse() + Startup.WindowsAuthRelativePath));
                    channel = factory.CreateChannel();
                    ((IChannel)channel).Open();
                    string result = channel.EchoString(testString);
                    Assert.Equal(testString, result);
                    ((IChannel)channel).Close();
                    factory.Close();
                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((IChannel)channel, factory);
                }
            }
        }

        [Fact]
        public void SimpleNetTcpClientConnection()
        {
            string testString = new string('a', 3000);
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                System.ServiceModel.ChannelFactory<ClientContract.ITestService> factory = null;
                ClientContract.ITestService channel = null;
                host.Start();
                try
                {
                    System.ServiceModel.NetTcpBinding binding = ClientHelper.GetStreamedModeBinding();
                    factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(binding,
                        new System.ServiceModel.EndpointAddress(host.GetNetTcpAddressInUse() + Startup.NoSecurityRelativePath));
                    channel = factory.CreateChannel();
                    ((IChannel)channel).Open();
                    string result = channel.EchoString(testString);
                    Assert.Equal(testString, result);
                    ((IChannel)channel).Close();
                    factory.Close();
                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((IChannel)channel, factory);
                }
            }
        }

        [Fact]
        public void MultipleClientsNonConcurrentNetTcpClientConnection()
        {
            string testString = new string('a', 3000);
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                System.ServiceModel.ChannelFactory<ClientContract.ITestService> factory = null;
                ClientContract.ITestService channel = null;
                host.Start();
                try
                {
                    System.ServiceModel.NetTcpBinding binding = ClientHelper.GetStreamedModeBinding();
                    factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(binding,
                        new System.ServiceModel.EndpointAddress(host.GetNetTcpAddressInUse() + Startup.NoSecurityRelativePath));
                    channel = factory.CreateChannel();
                    ((IChannel)channel).Open();
                    string result = channel.EchoString(testString);
                    Assert.Equal(testString, result);
                    ((IChannel)channel).Close();
                    channel = factory.CreateChannel();
                    ((IChannel)channel).Open();
                    result = channel.EchoString(testString);
                    Assert.Equal(testString, result);
                    ((IChannel)channel).Close();
                    factory.Close();
                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((IChannel)channel, factory);
                }
            }
        }

        [Fact]
        public void SingleClientMultipleRequestsNetTcpClientConnection()
        {
            string testString = new string('a', 3000);
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                System.ServiceModel.ChannelFactory<ClientContract.ITestService> factory = null;
                ClientContract.ITestService channel = null;
                host.Start();
                try
                {
                    System.ServiceModel.NetTcpBinding binding = ClientHelper.GetStreamedModeBinding();
                    factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(binding,
                        new System.ServiceModel.EndpointAddress(host.GetNetTcpAddressInUse() + Startup.NoSecurityRelativePath));
                    channel = factory.CreateChannel();
                    ((IChannel)channel).Open();
                    string result = channel.EchoString(testString);
                    Assert.Equal(testString, result);
                    result = channel.EchoString(testString);
                    Assert.Equal(testString, result);
                    ((IChannel)channel).Close();
                    factory.Close();
                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((IChannel)channel, factory);
                }
            }
        }

        [Fact]
        public void MultipleClientsUsingPooledSocket()
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                System.ServiceModel.ChannelFactory<ClientContract.ITestService> factory = null;
                ClientContract.ITestService channel = null;
                host.Start();
                try
                {
                    System.ServiceModel.NetTcpBinding binding = ClientHelper.GetStreamedModeBinding();
                    factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(binding,
                        new System.ServiceModel.EndpointAddress(host.GetNetTcpAddressInUse() + Startup.NoSecurityRelativePath));
                    channel = factory.CreateChannel();
                    ((IChannel)channel).Open();
                    string clientIpEndpoint = channel.GetClientIpEndpoint();
                    ((IChannel)channel).Close();
                    for (int i = 0; i < 10; i++)
                    {
                        channel = factory.CreateChannel();
                        ((IChannel)channel).Open();
                        string clientIpEndpoint2 = channel.GetClientIpEndpoint();
                        Assert.Equal(clientIpEndpoint, clientIpEndpoint2);
                        ((IChannel)channel).Close();
                    }
                    factory.Close();
                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((IChannel)channel, factory);
                }
            }
        }

        [Fact]
        public void SingleClientsUsingPooledSocketForMultipleRequests()
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                System.ServiceModel.ChannelFactory<ClientContract.ITestService> factory = null;
                ClientContract.ITestService channel = null;
                host.Start();
                try
                {
                    System.ServiceModel.NetTcpBinding binding = ClientHelper.GetStreamedModeBinding();
                    factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(binding,
                        new System.ServiceModel.EndpointAddress(host.GetNetTcpAddressInUse() + Startup.NoSecurityRelativePath));
                    channel = factory.CreateChannel();
                    ((IChannel)channel).Open();
                    string clientIpEndpoint = channel.GetClientIpEndpoint();
                    for (int i = 0; i < 10; i++)
                    {
                        string clientIpEndpoint2 = channel.GetClientIpEndpoint();
                        Assert.Equal(clientIpEndpoint, clientIpEndpoint2);
                    }
                ((IChannel)channel).Close();
                    factory.Close();
                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((IChannel)channel, factory);
                }
            }
        }

        [Fact]
        public void TwoStreamedServices()
        {
            string testString = new string('a', 3000);
            IWebHost host = ServiceHelper.CreateWebHostBuilder<StartupMultiService>(_output).Build();
            using (host)
            {
                System.ServiceModel.ChannelFactory<Contract.IEchoService> factory1 = null;
                System.ServiceModel.ChannelFactory<ClientContract.ITestService> factory2 = null;
                Contract.IEchoService channel1 = null;
                ClientContract.ITestService channel2 = null;
                host.Start();
                try
                {
                    System.ServiceModel.NetTcpBinding binding = ClientHelper.GetStreamedModeBinding();
                    factory1 = new System.ServiceModel.ChannelFactory<Contract.IEchoService>(binding,
                        new System.ServiceModel.EndpointAddress(host.GetNetTcpAddressInUse() + Startup.NoSecurityRelativePath + "/1"));
                    channel1 = factory1.CreateChannel();
                    ((IChannel)channel1).Open();
                    string response = channel1.EchoString(testString);
                    Assert.Equal(testString, response);

                    factory2 = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(binding,
                        new System.ServiceModel.EndpointAddress(host.GetNetTcpAddressInUse() + Startup.NoSecurityRelativePath + "/2"));
                    channel2 = factory2.CreateChannel();
                    ((IChannel)channel2).Open();
                    response = channel2.EchoString(testString);
                    Assert.Equal(testString, response);
                    ((IChannel)channel1).Close();
                    ((IChannel)channel2).Close();
                    factory1.Close();
                    factory2.Close();
                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((IChannel)channel1, (IChannel)channel2, factory1, factory2);
                }
            }
        }

        [Fact(Skip = "Takes a long time to run so don't want in regular test run")]
        public async Task LargeStreamsSucceed()
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<StartupStreamedService>(_output).Build();
            using (host)
            {
                System.ServiceModel.ChannelFactory<Contract.IStreamService> factory = null;
                Contract.IStreamService channel = null;
                host.Start();
                try
                {
                    System.ServiceModel.NetTcpBinding binding = ClientHelper.GetStreamedModeBinding();
                    binding.MaxReceivedMessageSize = long.MaxValue;
                    binding.MaxBufferSize = 1024 * 1024;
                    binding.ReaderQuotas = XmlDictionaryReaderQuotas.Max;
                    binding.SendTimeout = TimeSpan.FromHours(2);
                    binding.ReceiveTimeout = TimeSpan.FromHours(2);
                    binding.OpenTimeout = TimeSpan.FromHours(2);
                    binding.CloseTimeout = TimeSpan.FromHours(2);
                    factory = new System.ServiceModel.ChannelFactory<Contract.IStreamService>(binding,
                        new System.ServiceModel.EndpointAddress(host.GetNetTcpAddressInUse() + StartupStreamedService.NoSecurityRelativePath));
                    channel = factory.CreateChannel();
                    ((IChannel)channel).Open();
                    long testSize = (long)int.MaxValue + (1024 * 1024);
                    long receivedSize = await channel.SendStreamAsync(new FixedLengthDataGeneratingStream(testSize));
                    Assert.Equal(testSize, receivedSize);
                    var incomingStream = await channel.GetStreamAsync(testSize);
                    byte[] readBuffer = new byte[64 * 1024];
                    receivedSize = 0;
                    while(true)
                    {
                        int bytesRead = await incomingStream.ReadAsync(readBuffer, 0, readBuffer.Length);
                        if (bytesRead == 0) break;
                        receivedSize += bytesRead;
                    }
                    Assert.Equal(testSize, receivedSize);
                    ((IChannel)channel).Close();
                    factory.Close();
                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((IChannel)channel, factory);
                }
            }
        }

        public class Startup
        {
            public const string WindowsAuthRelativePath = "/nettcp.svc/windows-auth";
            public const string NoSecurityRelativePath = "/nettcp.svc/security-none";

            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.TestService>();
                    builder.AddServiceEndpoint<Services.TestService, ServiceContract.ITestService>(
                        new CoreWCF.NetTcpBinding
                        {
                            TransferMode = CoreWCF.TransferMode.Streamed
                        }, WindowsAuthRelativePath);
                    builder.AddServiceEndpoint<Services.TestService, ServiceContract.ITestService>(
                        new CoreWCF.NetTcpBinding(CoreWCF.SecurityMode.None)
                        {
                            TransferMode = CoreWCF.TransferMode.Streamed
                        }, NoSecurityRelativePath);
                });
            }
        }

        public class StartupMultiService
        {
            public const string NoSecurityRelativePath = "/nettcp.svc/security-none";

            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.EchoService>();
                    builder.AddServiceEndpoint<Services.EchoService, Contract.IEchoService>(
                        new CoreWCF.NetTcpBinding(CoreWCF.SecurityMode.None)
                        {
                            TransferMode = CoreWCF.TransferMode.Streamed
                        }, NoSecurityRelativePath + "/1");
                    builder.AddService<Services.TestService>();
                    builder.AddServiceEndpoint<Services.TestService, ServiceContract.ITestService>(
                        new CoreWCF.NetTcpBinding(CoreWCF.SecurityMode.None)
                        {
                            TransferMode = CoreWCF.TransferMode.Streamed
                        }, NoSecurityRelativePath + "/2");
                });
            }
        }

        public class StartupStreamedService
        {
            public const string NoSecurityRelativePath = "/nettcp.svc/security-none";

            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.StreamService>();
                    builder.AddServiceEndpoint<Services.StreamService, Contract.IStreamService>(
                        new CoreWCF.NetTcpBinding(CoreWCF.SecurityMode.None)
                        {
                            TransferMode = CoreWCF.TransferMode.Streamed,
                            MaxReceivedMessageSize = long.MaxValue,
                            MaxBufferSize = 1024 * 1024,
                            ReaderQuotas = XmlDictionaryReaderQuotas.Max,
                            SendTimeout = TimeSpan.FromHours(2),
                            ReceiveTimeout = TimeSpan.FromHours(2),
                            OpenTimeout = TimeSpan.FromHours(2),
                            CloseTimeout = TimeSpan.FromHours(2)
                        }, NoSecurityRelativePath);
                });
            }
        }
    }
}
