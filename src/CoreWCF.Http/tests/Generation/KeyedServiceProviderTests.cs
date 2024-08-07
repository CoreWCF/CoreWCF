// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text.Json.Serialization;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.GeneratedOperationInvokers.Tests.Generation;

public partial class KeyedServiceProviderTests
{
    private readonly ITestOutputHelper _output;

    public KeyedServiceProviderTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void BasicHttpRequestReplyEchoString()
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
        using (host)
        {
            host.Start();
            System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
            var factory = new System.ServiceModel.ChannelFactory<IMyKeyedServiceContract>(httpBinding,
                new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/BasicWcfService/basichttp.svc")));
            IMyKeyedServiceContract channel = factory.CreateChannel();
            string result = channel.Hello("John");
            Assert.Equal("Bonjour John", result);
        }
    }

    [System.ServiceModel.ServiceContract]
    public interface IMyKeyedServiceContract
    {
        [System.ServiceModel.OperationContract]
        string Hello(string value);
    }

    [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
    public partial class MyKeyedServiceContract : IMyKeyedServiceContract
    {
        public string Hello(string value, [Injected(ServiceKey = "fr")] object o)
        {
            return o + value;
        }
    }

    internal class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddServiceModelServices();
            services.AddTransient<MyKeyedServiceContract>();
            services.AddKeyedTransient<object>("fr", (provider, key) => "Bonjour ");
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseServiceModel(builder =>
            {
                builder.AddService<MyKeyedServiceContract>();
                builder.AddServiceEndpoint<MyKeyedServiceContract, IMyKeyedServiceContract>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
            });
        }
    }
}
