// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests.Generation;

public partial class KeyedServiceProviderTests
{
    private readonly ITestOutputHelper _output;

    public KeyedServiceProviderTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public class GetInjectedKeyedServiceTestTheoryData : TheoryData<Type>
    {
        public GetInjectedKeyedServiceTestTheoryData()
        {
            Add(typeof(Startup<MyKeyedServiceContract>));
            Add(typeof(Startup<MyOtherKeyedServiceContract>));
        }
    }

    [Net8OrGreaterTheory]
    [ClassData(typeof(GetInjectedKeyedServiceTestTheoryData))]
    public void InjectedKeyedServiceTests(Type startupType)
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder(_output, startupType).Build();
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
        public string Hello(string value, [Injected(ServiceKey = "fr")] HelloProvider o) => $"{o.Invoke()} {value}";
    }

    [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
    public partial class MyOtherKeyedServiceContract : IMyKeyedServiceContract
    {
        public string Hello(string value, [FromKeyedServices("fr")] HelloProvider o) => $"{o.Invoke()} {value}";
    }

    public delegate string HelloProvider();

    internal class Startup<T> where T : class, IMyKeyedServiceContract
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddServiceModelServices();
            services.AddTransient<T>();
            services.AddKeyedTransient<HelloProvider>("fr", (_, _) => () => "Bonjour");
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseServiceModel(builder =>
            {
                builder.AddService<T>();
                builder.AddServiceEndpoint<T, IMyKeyedServiceContract>(new BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
            });
        }
    }
}
