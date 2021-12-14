// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests
{
    public class RequestPathModificationTests
    {
        private readonly ITestOutputHelper _output;

        public RequestPathModificationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task VerifyPathRestoredAsync()
        {
            string testString = new string('a', 100);
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasePath/BasicHttp/basichttp.svc")));
                ClientContract.IEchoService channel = factory.CreateChannel();
                string result = channel.EchoString(testString);
                Assert.Equal(testString, result);
                HttpClient httpClient = new HttpClient();
                var responseMessage = await httpClient.GetAsync("http://localhost:8080/BasePath/SomeOtherUrl/GetRequest");
                Assert.True(responseMessage.Headers.Contains("Test_Path"));
                Assert.Equal("/SomeOtherUrl/GetRequest", responseMessage.Headers.GetValues("Test_Path").SingleOrDefault());
                Assert.True(responseMessage.Headers.Contains("Test_PathBase"));
                Assert.Equal("/BasePath", responseMessage.Headers.GetValues("Test_PathBase").SingleOrDefault());
            }
        }

        internal class Startup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public void Configure(IApplicationBuilder app, IHostingEnvironment env)
            {
                app.UsePathBase("/BasePath");
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.EchoService>();
                    builder.AddServiceEndpoint<Services.EchoService, ServiceContract.IEchoService>(new CoreWCF.BasicHttpBinding(), "/BasePath/BasicHttp/basichttp.svc");
                });
                app.Use(next =>
                {
                    return reqContext =>
                    {
                        reqContext.Response.Headers["Test_Path"] = reqContext.Request.Path.ToString();
                        reqContext.Response.Headers["Test_PathBase"] = reqContext.Request.PathBase.ToString();
                        return next(reqContext);
                    };
                });
            }
        }
    }
}
