// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.NetTcp.Tests;

public class Ipv6Tests
{
    private static readonly Random random = new Random();

    [Fact]
    public void DefaultUseNetTcpBindsToIPv6()
    {
        string testString = new string('a', 3000);
        int port = random.Next(1025, 65535);
        using var host = WebHost.CreateDefaultBuilder(Array.Empty<string>())
            .UseStartup<Startup>()
            .UseNetTcp(port)
            .Build();


        host.Start();
        var netTcpBinding = ClientHelper.GetBufferedModeBinding();
        using var factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(netTcpBinding,
            new System.ServiceModel.EndpointAddress(new Uri($"net.tcp://[::1]:{port}/EchoService.svc")));
        var channel = factory.CreateChannel();


        var result = channel.EchoString(testString);
        Assert.Equal(testString, result);
    }

    [Fact]
    public void RegressionBoundToIpv6AnyStillAllowsIpv4()
    {
        string testString = new string('a', 3000);
        int port = random.Next(1025, 65535);
        using var host = WebHost.CreateDefaultBuilder(Array.Empty<string>())
            .UseStartup<Startup>()
            .UseNetTcp(port)
            .Build();


        host.Start();
        var netTcpBinding = ClientHelper.GetBufferedModeBinding();
        using var factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(netTcpBinding,
            new System.ServiceModel.EndpointAddress(new Uri($"net.tcp://127.0.0.1:{port}/EchoService.svc")));
        var channel = factory.CreateChannel();



        var result = channel.EchoString(testString);
        Assert.Equal(testString, result);
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
                builder.AddService<Services.TestService>();
                builder.AddServiceEndpoint<Services.TestService, ServiceContract.ITestService>(new NetTcpBinding(SecurityMode.None), "/EchoService.svc");
            });
        }
    }
}
