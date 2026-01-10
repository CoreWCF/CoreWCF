// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
    public class SessionTests
    {
        private readonly ITestOutputHelper _output;

        public SessionTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        // A basic test to verify that a session gets created and terminated
        public void Test_IsInitiating_IsTerminating()
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                System.ServiceModel.ChannelFactory<ISessionTestClient.ISessionTest> factory = null;
                ISessionTestClient.ISessionTest channel = null;
                host.Start();
                try
                {
                    // *** SETUP *** \\
                    System.ServiceModel.NetTcpBinding binding = new System.ServiceModel.NetTcpBinding(System.ServiceModel.SecurityMode.None);
                    factory = new System.ServiceModel.ChannelFactory<ISessionTestClient.ISessionTest>(binding,
                        new System.ServiceModel.EndpointAddress(host.GetNetTcpAddressInUse() + Startup.SessionRelativeAddress));
                    channel = factory.CreateChannel();
                    const int A = 0xAAA;
                    const int B = 0xBBB;

                    // *** EXECUTE *** \\
                    var dataA = channel.MethodAInitiating(A);
                    // MethodA is initiating so now we have a session and can call non initiating MethodB
                    var dataB = channel.MethodBNonInitiating(B);
                    var dataC = channel.MethodCTerminating();

                    // *** VALIDATE *** \\
                    Assert.Equal(A, dataA);
                    Assert.Equal(B, dataB);

                    // *** CLEANUP *** \\
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
        public void Test_IsInitiating_IsTerminating_Separate_Channels()
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                System.ServiceModel.ChannelFactory<ISessionTestClient.ISessionTest> factory = null;
                ISessionTestClient.ISessionTest channel1 = null, channel2 = null;
                host.Start();
                try
                {
                    // *** SETUP *** \\
                    System.ServiceModel.NetTcpBinding binding = new System.ServiceModel.NetTcpBinding(System.ServiceModel.SecurityMode.None);
                    factory = new System.ServiceModel.ChannelFactory<ISessionTestClient.ISessionTest>(binding,
                        new System.ServiceModel.EndpointAddress(host.GetNetTcpAddressInUse() + Startup.SessionRelativeAddress));
                    channel1 = factory.CreateChannel();
                    channel2 = factory.CreateChannel();
                    const int A1 = 0xA1, B1 = 0xB1;
                    const int A2 = 0xA2, B2 = 0xB2;

                    // *** EXECUTE *** \\
                    var dataA1 = channel1.MethodAInitiating(A1);
                    var dataA2 = channel2.MethodAInitiating(A2);
                    var dataB1 = channel1.MethodBNonInitiating(B1);
                    var dataB2 = channel2.MethodBNonInitiating(B2);
                    var sessionId1 = ((System.ServiceModel.IClientChannel)channel1).SessionId;
                    var sessionId2 = ((System.ServiceModel.IClientChannel)channel2).SessionId;
                    var dataC1 = channel1.MethodCTerminating();
                    var dataC2 = channel2.MethodCTerminating();

                    // *** VALIDATE *** \\
                    Assert.Equal(A1, dataA1);
                    Assert.Equal(B1, dataB1);
                    Assert.Equal(A2, dataA2);
                    Assert.Equal(B2, dataB2);

                    // The session ids should be different for 2 different channels
                    Assert.NotEqual(sessionId1, sessionId2);

                    // *** CLEANUP *** \\
                    ((IChannel)channel1).Close();
                    ((IChannel)channel2).Close();
                    factory.Close();
                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((IChannel)channel1, (IChannel)channel2, factory);
                }
            }
        }

        [Fact]
        public void Test_Negative_Calling_NonInitiating_Method_First()
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                System.ServiceModel.ChannelFactory<ISessionTestClient.ISessionTest> factory = null;
                ISessionTestClient.ISessionTest channel = null;
                host.Start();
                try
                {
                    // *** SETUP *** \\
                    System.ServiceModel.NetTcpBinding binding = new System.ServiceModel.NetTcpBinding(System.ServiceModel.SecurityMode.None);
                    factory = new System.ServiceModel.ChannelFactory<ISessionTestClient.ISessionTest>(binding,
                        new System.ServiceModel.EndpointAddress(host.GetNetTcpAddressInUse() + Startup.SessionRelativeAddress));
                    channel = factory.CreateChannel();

                    // *** EXECUTE *** \\
                    Assert.Throws<System.ServiceModel.ActionNotSupportedException>(() =>
                    {
                        channel.MethodBNonInitiating(1);
                    });

                    // *** CLEANUP *** \\
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
        public void Test_IsInitiating_NonInitiating_Separate_Channels()
        {
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                System.ServiceModel.ChannelFactory<ISessionTestClient.ISessionTest> factory = null;
                ISessionTestClient.ISessionTest channel1 = null, channel2 = null;
                host.Start();
                try
                {
                    // *** SETUP *** \\
                    System.ServiceModel.NetTcpBinding binding = new System.ServiceModel.NetTcpBinding(System.ServiceModel.SecurityMode.None);
                    factory = new System.ServiceModel.ChannelFactory<ISessionTestClient.ISessionTest>(binding,
                        new System.ServiceModel.EndpointAddress(host.GetNetTcpAddressInUse() + Startup.SessionRelativeAddress));
                    channel1 = factory.CreateChannel();
                    channel2 = factory.CreateChannel();
                    const int A1 = 0xA1, A2 = 0xA2;

                    // *** EXECUTE *** \\
                    var dataA1 = channel1.MethodAInitiating(A1);
                    var sessionId1 = ((System.ServiceModel.IClientChannel)channel1).SessionId;
                    var sessionId2 = ((System.ServiceModel.IClientChannel)channel2).SessionId;

                    Assert.Throws<System.ServiceModel.ActionNotSupportedException>(() =>
                    {
                        channel2.MethodBNonInitiating(A2);
                    });
                    
                    var dataC1 = channel1.MethodCTerminating();

                    // *** VALIDATE *** \\
                    Assert.Equal(A1, dataA1);

                    // The session ids should be different for 2 different channels
                    Assert.NotEqual(sessionId1, sessionId2);

                    // *** CLEANUP *** \\
                    ((IChannel)channel1).Close();
                    ((IChannel)channel2).Close();
                    factory.Close();
                }
                finally
                {
                    ServiceHelper.CloseServiceModelObjects((IChannel)channel1, (IChannel)channel2, factory);
                }
            }
        }

        public class Startup
        {
            public const string SessionRelativeAddress = "/nettcp.session.svc/";

            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<ISessionTestService.SessionTestService>();
                    builder.AddServiceEndpoint<ISessionTestService.SessionTestService, ISessionTestService.ISessionTest>(new NetTcpBinding(SecurityMode.None), SessionRelativeAddress);
                });
            }
        }
    }
}
