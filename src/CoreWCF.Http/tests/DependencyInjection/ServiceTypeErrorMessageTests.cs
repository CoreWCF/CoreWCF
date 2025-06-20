// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Helpers;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests.DependencyInjection
{
    public class ServiceTypeErrorMessageTests
    {
        private readonly ITestOutputHelper _output;

        public ServiceTypeErrorMessageTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void ServiceWithoutDefaultConstructorAndNotInDI_ShowsServiceTypeInErrorMessage()
        {
            // Arrange - Create a host with a service that has no default constructor and is not registered in DI
            IWebHost host = ServiceHelper.CreateWebHostBuilder<StartupWithProblematicService>(_output).Build();
            
            using (host)
            {
                // Act - Start the host (this should succeed)
                host.Start();
                
                // Create a client to make a request that will trigger service instantiation
                System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ITestService>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/testservice")));
                ITestService channel = factory.CreateChannel();
                
                // Assert - Making a request should throw a FaultException with the service type name
                var exception = Assert.Throws<System.ServiceModel.FaultException<System.ServiceModel.ExceptionDetail>>(() => channel.GetData("test"));
                
                // Verify that the error message contains the full type name
                Assert.Contains(typeof(ServiceWithNoDefaultConstructor).FullName, exception.Detail.Message);
            }
        }

        [Fact]
        public void SingletonServiceWithoutDefaultConstructorAndNotInDI_ShowsServiceTypeInErrorMessage()
        {
            // Arrange - Create a host with a singleton service that has no default constructor and is not registered in DI
            IWebHost host = ServiceHelper.CreateWebHostBuilder<StartupWithProblematicSingletonService>(_output).Build();
            
            using (host)
            {
                // Act - Start the host (this should succeed)
                host.Start();
                
                // Create a client to make a request that will trigger service instantiation
                System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ITestService>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/testservice")));
                ITestService channel = factory.CreateChannel();
                
                // Assert - Making a request should throw a FaultException with the service type name
                var exception = Assert.Throws<System.ServiceModel.FaultException<System.ServiceModel.ExceptionDetail>>(() => channel.GetData("test"));
                
                // Verify that the error message contains the full type name
                Assert.Contains(typeof(SingletonServiceWithNoDefaultConstructor).FullName, exception.Detail.Message);
            }
        }

        private class StartupWithProblematicService
        {
            public void ConfigureServices(IServiceCollection services)
            {
                // Note: We're NOT registering ServiceWithNoDefaultConstructor in DI
                // This will cause the error when the service is tried to be instantiated
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    // This will fail because ServiceWithNoDefaultConstructor is not in DI and has no default constructor
                    // Using ServiceOptions to enable exception detail in faults so we can verify the error message
                    builder.AddService<ServiceWithNoDefaultConstructor>(options =>
                    {
                        options.DebugBehavior.IncludeExceptionDetailInFaults = true;
                    });
                    builder.AddServiceEndpoint<ServiceWithNoDefaultConstructor, ITestService>(new BasicHttpBinding(), "/testservice");
                });
            }
        }

        [System.ServiceModel.ServiceContract]
        internal interface ITestService
        {
            [System.ServiceModel.OperationContract]
            string GetData(string value);
        }

        private class StartupWithProblematicSingletonService
        {
            public void ConfigureServices(IServiceCollection services)
            {
                // Note: We're NOT registering SingletonServiceWithNoDefaultConstructor in DI
                // This will cause the error when the service is tried to be instantiated
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    // This will fail because SingletonServiceWithNoDefaultConstructor is not in DI and has no default constructor
                    // Using ServiceOptions to enable exception detail in faults so we can verify the error message
                    builder.AddService<SingletonServiceWithNoDefaultConstructor>(options =>
                    {
                        options.DebugBehavior.IncludeExceptionDetailInFaults = true;
                    });
                    builder.AddServiceEndpoint<SingletonServiceWithNoDefaultConstructor, ITestService>(new BasicHttpBinding(), "/testservice");
                });
            }
        }

        // Test service without default constructor - this will cause the error
        private class ServiceWithNoDefaultConstructor : ITestService
        {
            private readonly string _dependency;

            public ServiceWithNoDefaultConstructor(string dependency)
            {
                _dependency = dependency;
            }

            public string GetData(string value) => $"Data: {value}";
        }

        // Test singleton service without default constructor - this will cause the error in a different code path
        [ServiceBehavior(InstanceContextMode = InstanceContextMode.Single)]
        private class SingletonServiceWithNoDefaultConstructor : ITestService
        {
            private readonly string _dependency;

            public SingletonServiceWithNoDefaultConstructor(string dependency)
            {
                _dependency = dependency;
            }

            public string GetData(string value) => $"Data: {value}";
        }
    }
}