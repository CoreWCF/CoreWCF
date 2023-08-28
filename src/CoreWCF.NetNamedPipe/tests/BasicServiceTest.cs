// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Contract;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Services;
using Xunit;
using Xunit.Abstractions;

public class BasicServiceTest
{
    public ITestOutputHelper _output;

    public BasicServiceTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void NetPipeRequestReplyEchoString()
    {
        string testString = new string('a', 3000);
        var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();

        using (host)
        {
            host.Start();
            var binding = new System.ServiceModel.NetNamedPipeBinding();
            var factory = new System.ServiceModel.ChannelFactory<IEchoService>(binding,
                new System.ServiceModel.EndpointAddress(new Uri($"net.pipe://localhost/{nameof(NetPipeRequestReplyEchoString)}/netpipe.svc")));
            var channel = factory.CreateChannel();
            System.ServiceModel.Channels.IChannel ichannel = (System.ServiceModel.Channels.IChannel)channel;
            ichannel.Open();
            var result = channel.EchoString(testString);
            Assert.Equal(testString, result);
            ichannel.Close();
        }
    }

    [Fact]
    public void NetPipeLongPathHashesCorrectly()
    {
        string testString = new string('a', 3000);
        // Path is hashed if it's longer than 128 bytes
        string basePath = $"{nameof(NetPipeLongPathHashesCorrectly)}{new string('a', 128)}";
        var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output, basePath).Build();

        using (host)
        {
            host.Start();
            var binding = new System.ServiceModel.NetNamedPipeBinding();
            var factory = new System.ServiceModel.ChannelFactory<IEchoService>(binding,
                new System.ServiceModel.EndpointAddress(new Uri($"net.pipe://localhost/{basePath}/netpipe.svc")));
            var channel = factory.CreateChannel();
            System.ServiceModel.Channels.IChannel ichannel = (System.ServiceModel.Channels.IChannel)channel;
            ichannel.Open();
            var result = channel.EchoString(testString);
            Assert.Equal(testString, result);
            ichannel.Close();
        }
    }

    [Fact]
    public void NetPipeStopAndRestartSameAddress()
    {
        // Test to validate issue #1117 doesn't regress
        string testString = new string('a', 3000);
        var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
        var clientBinding = new System.ServiceModel.NetNamedPipeBinding();
        var customClientBinding = new System.ServiceModel.Channels.CustomBinding(clientBinding);
        var namedPipeTransport = customClientBinding.Elements.Find<System.ServiceModel.Channels.NamedPipeTransportBindingElement>();
        // Disable connection pooling as currently the service doesn't cleanly close the pipe on shutdown.
        namedPipeTransport.ConnectionPoolSettings.MaxOutboundConnectionsPerEndpoint = 0;
        var factory = new System.ServiceModel.ChannelFactory<IEchoService>(customClientBinding,
            new System.ServiceModel.EndpointAddress(new Uri($"net.pipe://localhost/{nameof(NetPipeStopAndRestartSameAddress)}/netpipe.svc")));

        using (host)
        {
            host.Start();
            var channel = factory.CreateChannel();
            System.ServiceModel.Channels.IChannel ichannel = (System.ServiceModel.Channels.IChannel)channel;
            ichannel.Open();
            var result = channel.EchoString(testString);
            Assert.Equal(testString, result);
            ichannel.Close();
        }

        //Host is stopped, now restart it again using the same listen address
        host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
        using (host)
        {
            host.Start();
            var channel = factory.CreateChannel();
            System.ServiceModel.Channels.IChannel ichannel = (System.ServiceModel.Channels.IChannel)channel;
            ichannel.Open();
            var result = channel.EchoString(testString);
            Assert.Equal(testString, result);
            ichannel.Close();
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
                builder.AddService<EchoService>();
                builder.AddServiceEndpoint<EchoService, IEchoService>(new CoreWCF.NetNamedPipeBinding(), "netpipe.svc");
            });
        }
    }
}
