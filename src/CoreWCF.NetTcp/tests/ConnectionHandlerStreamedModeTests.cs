// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ServiceModel.Channels;
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

        [Fact]
        [Trait("Category", "WindowsOnly")]
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

        public class Startup
        {
            public const string WindowsAuthRelativePath = "/nettcp.svc/windows-auth";
            public const string NoSecurityRelativePath = "/nettcp.svc/security-none";

            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app, IHostingEnvironment env)
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
    }
}