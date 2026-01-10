// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using CoreWCF.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Helpers;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests;

public class KeyedServicesTests
{
    private readonly ITestOutputHelper _output;

    public KeyedServicesTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void BasicHttpRequestReplyEchoString()
    {
        string testString = "azerty";
        IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
        using (host)
        {
            host.Start();
            System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
            var factory = new System.ServiceModel.ChannelFactory<IReverseEchoService>(httpBinding,
                new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/service")));
            IReverseEchoService channel = factory.CreateChannel();
            string result = channel.Reverse(testString);
            Assert.Equal("ytreza", result);
        }
    }

    private class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<ReverseEchoService>();
            Func<string, string> reverse = value => new string(value.Reverse().ToArray());
            services.AddKeyedSingleton(typeof(Func<string, string>), "reverse", (_, _) => reverse);
            services.AddServiceModelServices();
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseServiceModel(builder =>
            {
                builder.AddService<ReverseEchoService>();
                builder.AddServiceEndpoint<ReverseEchoService, IReverseEchoService>(new BasicHttpBinding(), "/service");
            });
        }
    }

    [System.ServiceModel.ServiceContract]
    internal interface IReverseEchoService
    {
        [System.ServiceModel.OperationContract]
        string Reverse(string value);
    }

    [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
    private class ReverseEchoService : IReverseEchoService
    {
        private readonly Func<string, string> _reverse;

        public ReverseEchoService([FromKeyedServices("reverse")] Func<string, string> reverse)
        {
            _reverse = reverse;
        }

        public string Reverse(string value) => _reverse.Invoke(value);
    }
}

