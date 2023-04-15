// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Description;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests
{
    public class ExtensibilityFailureTests
    {
        private readonly ITestOutputHelper _output;

        public ExtensibilityFailureTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task ThrowingExtensibilityReturnsFaultAsync()
        {
            string testString = new('a', 3000);
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                await host.StartAsync();
                System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/BasicWcfService/basichttp.svc")));
                ClientContract.IEchoService channel = factory.CreateChannel();
                var ex = Assert.Throws<System.ServiceModel.FaultException<System.ServiceModel.ExceptionDetail>>(() => channel.EchoString(testString));
                Assert.Equal("InternalServiceFault", ex.Code.Name);
                Assert.Equal("http://schemas.microsoft.com/net/2005/12/windowscommunicationfoundation/dispatcher", ex.Code.Namespace);
                await host.StopAsync();
            }
            // Work around bug in aspnetcore where they can write to the ILogger in the Heartbeat timer loop
            // The implementation of Heartbeat as of April 2023 runs it every 1 second. Adding a delay slightly
            // longer than 1 second to allow it to run and log a failure before the test ends. See aspnetcore
            // issue for more details: https://github.com/dotnet/aspnetcore/issues/47577
            await Task.Delay(1100);
        }

        internal class Startup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
                services.AddSingleton<IServiceBehavior, ThrowingOperationSelectorBehavior>();
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.EchoService>();
                    builder.AddServiceEndpoint<Services.EchoService, ServiceContract.IEchoService>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
                });
            }
        }

        internal class ThrowingOperationSelectorBehavior : IServiceBehavior
        {
            public void AddBindingParameters(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase, Collection<ServiceEndpoint> endpoints, BindingParameterCollection bindingParameters) { }
            public void ApplyDispatchBehavior(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase)
            {
                foreach(var cdb in serviceHostBase.ChannelDispatchers)
                {
                    var cd = cdb as Dispatcher.ChannelDispatcher;
                    foreach(var endpoint in cd.Endpoints)
                    {
                        endpoint.DispatchRuntime.OperationSelector = new ThrowingOperationSelector();
                    }
                }
            }
            public void Validate(ServiceDescription serviceDescription, ServiceHostBase serviceHostBase) { }

            private class ThrowingOperationSelector : Dispatcher.IDispatchOperationSelector
            {
                public string SelectOperation(ref Message message)
                {
                    throw new Exception("failed");
                }
            }
        }
    }
}
