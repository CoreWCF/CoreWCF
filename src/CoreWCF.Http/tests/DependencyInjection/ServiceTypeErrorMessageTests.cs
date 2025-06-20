// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
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
                
                // Assert - Making a request should throw an exception with the service type name
                var exception = Assert.ThrowsAny<Exception>(() => channel.GetData("test"));
                
                // Output detailed exception information for debugging
                _output.WriteLine($"Exception type: {exception.GetType().FullName}");
                _output.WriteLine($"Exception message: {exception.Message}");
                if (exception.InnerException != null)
                {
                    _output.WriteLine($"Inner exception type: {exception.InnerException.GetType().FullName}");
                    _output.WriteLine($"Inner exception message: {exception.InnerException.Message}");
                }
                
                // Verify that the error message contains the full type name
                // The exception might be wrapped, so check the entire exception chain
                string fullExceptionMessage = GetFullExceptionMessage(exception);
                _output.WriteLine($"Full exception message: {fullExceptionMessage}");
                
                // Check for both the full name and just the class name
                Assert.True(fullExceptionMessage.Contains(typeof(ServiceWithNoDefaultConstructor).FullName) ||
                           fullExceptionMessage.Contains("ServiceWithNoDefaultConstructor"),
                           $"Expected service type name to be in exception message. Full message: {fullExceptionMessage}");
            }
        }

        private static string GetFullExceptionMessage(Exception exception)
        {
            var messages = new List<string>();
            var current = exception;
            while (current != null)
            {
                messages.Add(current.Message);
                current = current.InnerException;
            }
            return string.Join(" ", messages);
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