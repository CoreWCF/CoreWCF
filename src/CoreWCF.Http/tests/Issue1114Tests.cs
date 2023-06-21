// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Claims;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Description;
using CoreWCF.IdentityModel.Claims;
using CoreWCF.IdentityModel.Policy;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests;

public class Issue1114Tests
{

    private readonly ITestOutputHelper _outputHelper;

    public Issue1114Tests(ITestOutputHelper outputHelper)
    {
        _outputHelper = outputHelper;
    }

    [Fact]
    public void ServiceAuthorizationBehaviorPerServiceTest()
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_outputHelper).Build();
        using (host)
        {
            host.Start();
            System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
            var customFactory = new System.ServiceModel.ChannelFactory<IMyCustomService>(httpBinding,
                new System.ServiceModel.EndpointAddress(
                    new Uri($"http://localhost:{host.GetHttpPort()}/MyCustomService.svc")));
            IMyCustomService customChannel = customFactory.CreateChannel();
            customChannel.Echo("Hello World!");

            var otherFactory = new System.ServiceModel.ChannelFactory<IMyOtherService>(httpBinding,
                new System.ServiceModel.EndpointAddress(
                    new Uri($"http://localhost:{host.GetHttpPort()}/MyOtherService.svc")));
            IMyOtherService otherChannel = otherFactory.CreateChannel();
            otherChannel.Echo("Hello World!");

            var thirdFactory = new System.ServiceModel.ChannelFactory<IMyThirdService>(httpBinding,
                new System.ServiceModel.EndpointAddress(
                    new Uri($"http://localhost:{host.GetHttpPort()}/MyThirdService.svc")));
            IMyThirdService thirdChannel = thirdFactory.CreateChannel();
            thirdChannel.Echo("Hello World!");

            Spy spy = host.Services.GetService<Spy>();
            Assert.Equal(1, spy.CustomAuthorizationPolicyCallCount);
            Assert.Equal(1, spy.OtherAuthorizationPolicyCallCount);
        }
    }

    [CoreWCF.ServiceContract]
    [System.ServiceModel.ServiceContract]
    internal interface IMyCustomService
    {
        [CoreWCF.OperationContract]
        [System.ServiceModel.OperationContract]
        string Echo(string message);
    }

    [CoreWCF.ServiceContract]
    [System.ServiceModel.ServiceContract]
    internal interface IMyOtherService
    {
        [CoreWCF.OperationContract]
        [System.ServiceModel.OperationContract]
        string Echo(string message);
    }

    [CoreWCF.ServiceContract]
    [System.ServiceModel.ServiceContract]
    internal interface IMyThirdService
    {
        [CoreWCF.OperationContract]
        [System.ServiceModel.OperationContract]
        string Echo(string message);
    }

    [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
    internal class MyCustomService : IMyCustomService
    {
        public string Echo(string message) => message;
    }

    [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
    internal class MyOtherService : IMyOtherService
    {
        public string Echo(string message) => message;
    }

    [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
    internal class MyThirdService : IMyThirdService
    {
        public string Echo(string message) => message;
    }

    internal class MyCustomAuthorizationPolicy : IAuthorizationPolicy
    {
        private readonly Spy _spy;

        public MyCustomAuthorizationPolicy(Spy spy)
        {
            _spy = spy;
        }

        public string Id { get; }
        public ClaimSet Issuer { get; }

        public bool Evaluate(EvaluationContext evaluationContext, ref object state)
        {
            _spy.CustomAuthorizationPolicyCallCount++;
            evaluationContext.Properties.Add("Principal", new ClaimsPrincipal());
            return true;
        }
    }

    internal class MyOtherAuthorizationPolicy : IAuthorizationPolicy
    {
        private readonly Spy _spy;

        public MyOtherAuthorizationPolicy(Spy spy)
        {
            _spy = spy;
        }

        public string Id { get; }
        public ClaimSet Issuer { get; }

        public bool Evaluate(EvaluationContext evaluationContext, ref object state)
        {
            _spy.OtherAuthorizationPolicyCallCount++;
            evaluationContext.Properties.Add("Principal", new ClaimsPrincipal());
            return true;
        }
    }

    internal class Spy
    {
        public int CustomAuthorizationPolicyCallCount { get; set; } = 0;
        public int OtherAuthorizationPolicyCallCount { get; set; } = 0;
    }

    internal class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddServiceModelServices();
            services.AddSingleton<Spy>();
        }

        public void Configure(IApplicationBuilder app)
        {
            Spy spy = app.ApplicationServices.GetService<Spy>();
            app.UseServiceModel(builder =>
            {
                builder.AddService<MyCustomService>();
                builder.AddServiceEndpoint<MyCustomService, IMyCustomService>(
                    new BasicHttpBinding(BasicHttpSecurityMode.None), "/MyCustomService.svc");
                builder.AddService<MyOtherService>();
                builder.AddServiceEndpoint<MyOtherService, IMyOtherService>(
                    new BasicHttpBinding(BasicHttpSecurityMode.None), "/MyOtherService.svc");
                builder.AddService<MyThirdService>();
                builder.AddServiceEndpoint<MyThirdService, IMyThirdService>(
                    new BasicHttpBinding(BasicHttpSecurityMode.None), "/MyThirdService.svc");
                builder.ConfigureAllServiceHostBase(serviceHost =>
                {
                    if (serviceHost.Description.ServiceType == typeof(MyCustomService))
                    {
                        serviceHost.Description.Behaviors
                            .Remove<ServiceAuthorizationBehavior>();
                        serviceHost.Description.Behaviors.Add(new ServiceAuthorizationBehavior
                        {
                            ExternalAuthorizationPolicies =
                                new ReadOnlyCollection<IAuthorizationPolicy>(
                                    new List<IAuthorizationPolicy> { new MyCustomAuthorizationPolicy(spy) }),
                            PrincipalPermissionMode = PrincipalPermissionMode.Custom,
                        });

                        return;
                    }

                    if (serviceHost.Description.ServiceType == typeof(MyOtherService))
                    {
                        serviceHost.Description.Behaviors
                            .Remove<ServiceAuthorizationBehavior>();
                        serviceHost.Description.Behaviors.Add(new ServiceAuthorizationBehavior
                        {
                            ExternalAuthorizationPolicies =
                                new ReadOnlyCollection<IAuthorizationPolicy>(
                                    new List<IAuthorizationPolicy> { new MyOtherAuthorizationPolicy(spy) }),
                            PrincipalPermissionMode = PrincipalPermissionMode.Custom,
                        });
                    }
                });
            });
        }
    }
}
