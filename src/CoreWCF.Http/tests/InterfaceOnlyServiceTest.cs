// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace BasicHttp
{
    public class InterfaceOnlyServiceTest
    {
        private readonly ITestOutputHelper _output;

        public InterfaceOnlyServiceTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void BasicHttpRequestReplyEchoString()
        {
            string testString = new string('a', 3000);
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(httpBinding,
                    new System.ServiceModel.EndpointAddress(
                        new Uri("http://localhost:8080/BasicWcfService/basichttp.svc")));
                ClientContract.IEchoService channel = factory.CreateChannel();
                string result = channel.EchoString(testString);
                Assert.Equal(testString, result);

                var interceptor = host.Services.GetRequiredService<EchoServiceInterceptor>();
                Assert.Equal(1, interceptor.NumberOfIntercepts);
            }
        }

        private class EchoServiceInterceptor : ServiceContract.IEchoService
        {
            private readonly ServiceContract.IEchoService _underlyingService;

            public int NumberOfIntercepts { get; private set; }

            public EchoServiceInterceptor(ServiceContract.IEchoService underlyingService)
            {
                _underlyingService = underlyingService;
            }

            public string EchoString(string echo)
            {
                NumberOfIntercepts++;
                return _underlyingService.EchoString(echo);
            }

            public Stream EchoStream(Stream echo)
            {
                NumberOfIntercepts++;
                return _underlyingService.EchoStream(echo);
            }

            public async Task<string> EchoStringAsync(string echo)
            {
                NumberOfIntercepts++;
                return await _underlyingService.EchoStringAsync(echo);
            }

            public async Task<Stream> EchoStreamAsync(Stream echo)
            {
                NumberOfIntercepts++;
                return await _underlyingService.EchoStreamAsync(echo);
            }

            public string EchoToFail(string echo)
            {
                NumberOfIntercepts++;
                return _underlyingService.EchoToFail(echo);
            }
        }

        internal class Startup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
                services.AddSingleton(new EchoServiceInterceptor(new Services.EchoService()));
                // register service implementation in DI and let CoreWCF resolve it as instance
                // in some cases implementation could be without known type (e.g. could be a castle proxy)
                services.AddSingleton<ServiceContract.IEchoService>(sp => sp.GetRequiredService<EchoServiceInterceptor>());
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    // we fully register the service through interfaces
                    builder.AddService<ServiceContract.IEchoService>();
                    builder.AddServiceEndpoint<ServiceContract.IEchoService, ServiceContract.IEchoService>(
                        new CoreWCF.BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
                });
            }
        }
    }
}

