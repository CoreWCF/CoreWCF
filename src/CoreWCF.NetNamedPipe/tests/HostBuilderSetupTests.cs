// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using Contract;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Services;
using Xunit;
using Xunit.Abstractions;

public class HostBuilderSetupTests
{
    public ITestOutputHelper _output;

    public HostBuilderSetupTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task NetPipeRequestReplyEchoString()
    {
        string testString = new string('a', 3000);
        var host = ServiceHelper.CreateHostBuilder<Startup>(_output, $"{nameof(HostBuilderSetupTests)}/{nameof(NetPipeRequestReplyEchoString)}").Build();
        host.ConfigureUsingStartup<Startup>();

        using (host)
        {
            await host.StartAsync();
            var binding = new System.ServiceModel.NetNamedPipeBinding();
            var factory = new System.ServiceModel.ChannelFactory<IEchoService>(binding,
                new System.ServiceModel.EndpointAddress(new Uri($"net.pipe://localhost/{nameof(HostBuilderSetupTests)}/{nameof(NetPipeRequestReplyEchoString)}/netpipe.svc")));
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

        public void Configure(IHost host)
        {
            host.UseServiceModel(builder =>
            {
                builder.AddService<EchoService>();
                builder.AddServiceEndpoint<EchoService, IEchoService>(new CoreWCF.NetNamedPipeBinding(), "netpipe.svc");
            });
        }
    }
}
