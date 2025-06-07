// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using System.ServiceModel.Channels;
using CoreWCF.Channels;
using CoreWCF.Channels.Framing;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.NetTcp.Tests
{
    public class ServiceFramingFaultTests
    {
        private readonly ITestOutputHelper _output;

        public ServiceFramingFaultTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        // Validates when the client sends a message larger than the maximum allowed size, it returns the correct
        // fault message to the client, and on the service side doesn't result in an unhandled exception being
        // returned from the CoreWCF connection middleware.
        public void MaxMessageSizeExceeded_NoUnhandledExceptions()
        {
            string testString = new string('a', 10_000);
            var webHostBuilder = ServiceHelper.CreateWebHostBuilderWithoutNetTcp<Startup>(_output);
            var exceptionRecorder = new ExceptionRecorder();
            webHostBuilder.UseNetTcp(options =>
            {
                options.Listen("net.tcp://127.0.0.1:0", listenOptions =>
                {
                    listenOptions.Use(next =>
                    {
                        return async (ConnectionContext context) =>
                        {
                            try
                            {
                                await next(context);
                            }
                            catch (Exception ex)
                            {
                                // Catch any exceptions thrown by the service and record them
                                exceptionRecorder.Exceptions.Add(ex);
                                throw;
                            }
                        };
                    });
                });
            });
            var host = webHostBuilder.Build();
            using (host)
            {
                host.Start();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.ITestService>(new System.ServiceModel.NetTcpBinding(System.ServiceModel.SecurityMode.None),
                    new System.ServiceModel.EndpointAddress(host.GetNetTcpAddressInUse() + "/ServiceFaults.svc"));
                ClientContract.ITestService channel = factory.CreateChannel();
                ((System.ServiceModel.IClientChannel)channel).Open();
                var communicationException = Assert.Throws<System.ServiceModel.CommunicationException>(() =>
                {
                    channel.EchoString(testString);
                });
                Assert.IsType<System.ServiceModel.QuotaExceededException>(communicationException.InnerException);
                ServiceHelper.CloseServiceModelObjects((System.ServiceModel.IClientChannel)channel, factory);
                Assert.Empty(exceptionRecorder.Exceptions);
            }
        }

        public class ExceptionRecorder
        {
            public List<Exception> Exceptions { get; set; } = new List<Exception>();
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
                    builder.AddService<Services.TestService>();
                    var binding = new NetTcpBinding(SecurityMode.None);
                    binding.MaxReceivedMessageSize = 50;
                    builder.AddServiceEndpoint<Services.TestService, ServiceContract.ITestService>(binding, "/ServiceFaults.svc");
                });

            }
        }
    }
}
