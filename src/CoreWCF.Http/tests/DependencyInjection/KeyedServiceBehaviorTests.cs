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
using CoreWCF.Description;
using System.Collections.ObjectModel;
using CoreWCF.Channels;
using System.Collections.Generic;

namespace CoreWCF.Http.Tests;

public class KeyedServiceBehaviorTests
{
    private readonly ITestOutputHelper _output;

    public KeyedServiceBehaviorTests(ITestOutputHelper output)
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
            MyServiceBehavior serviceBehavior = host.Services.GetKeyedService<IServiceBehavior>(typeof(ReverseEchoService)) as MyServiceBehavior;

            System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
            var factory = new System.ServiceModel.ChannelFactory<IReverseEchoService>(httpBinding,
                new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/service")));
            IReverseEchoService channel = factory.CreateChannel();
            string result = channel.Reverse(testString);

            System.ServiceModel.BasicHttpBinding httpBinding2 = ClientHelper.GetBufferedModeBinding();
            var factory2 = new System.ServiceModel.ChannelFactory<IReverseEchoService>(httpBinding2,
                new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/service2")));
            IReverseEchoService channel2 = factory2.CreateChannel();
            string result2 = channel2.Reverse(testString);

            Assert.Contains(typeof(ReverseEchoService), serviceBehavior.AppliedServices);
            Assert.DoesNotContain(typeof(ReverseEchoService2), serviceBehavior.AppliedServices);
        }
    }

    private class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<ReverseEchoService>();
            services.AddSingleton<ReverseEchoService2>();
            services.AddKeyedSingleton<IServiceBehavior, MyServiceBehavior>(typeof(ReverseEchoService));
            services.AddServiceModelServices();
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseServiceModel(builder =>
            {
                builder.AddService<ReverseEchoService>();
                builder.AddServiceEndpoint<ReverseEchoService, IReverseEchoService>(new BasicHttpBinding(), "/service");
                builder.AddService<ReverseEchoService2>();
                builder.AddServiceEndpoint<ReverseEchoService2, IReverseEchoService>(new BasicHttpBinding(), "/service2");
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
        public string Reverse(string value) => new string(value.Reverse().ToArray());
    }

    [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
    private class ReverseEchoService2 : IReverseEchoService
    {
        public string Reverse(string value) => new string(value.Reverse().ToArray());
    }

    private class MyServiceBehavior : IServiceBehavior
    {
        public List<Type> AppliedServices { get; } = new();

        public void AddBindingParameters(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase, Collection<ServiceEndpoint> endpoints, BindingParameterCollection bindingParameters)
        {

        }

        public void ApplyDispatchBehavior(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase)
        {
            AppliedServices.Add(serviceDescription.ServiceType);
        }

        public void Validate(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase)
        {

        }
    }
}
