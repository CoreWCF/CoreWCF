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
            
            // Act & Assert - Starting the host should throw an exception with the service type name
            var exception = Assert.Throws<InvalidOperationException>(() => host.Start());
            
            // Verify that the error message contains the full type name
            Assert.Contains(typeof(ServiceWithNoDefaultConstructor).FullName, exception.Message);
            Assert.Contains("ServiceWithNoDefaultConstructor", exception.Message);
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
                    builder.AddService<ServiceWithNoDefaultConstructor>();
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
    }
}