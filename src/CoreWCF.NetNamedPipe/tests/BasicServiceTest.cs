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
