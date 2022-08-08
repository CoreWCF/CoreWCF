// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests
{
    public class HttpSysTests
    {
        private readonly ITestOutputHelper _output;

        public HttpSysTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        [Trait("Category", "WindowsOnly")]
        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        public void BasicHttpRequestReplyEchoString()
        {
            string testString = new string('a', 3000);
            IWebHost host = ServiceHelper.CreateHttpSysBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("http://localhost/Temporary_Listen_Addresses/CoreWCFTestServices/BasicWcfService/basichttp.svc")));
                ClientContract.IEchoService channel = factory.CreateChannel();
                string result = channel.EchoString(testString);
                Assert.Equal(testString, result);
                // Work around HttpSys host bug where it doesn't cancel a callback timer
                // and causes the host to write to the ILogger after the test has ended
                // which causes xunit to abort the test run. See dotnet/aspnetcore#30828
                var cts = new CancellationTokenSource();
                host.StopAsync(cts.Token).GetAwaiter().GetResult();
                cts.Cancel();
                cts.Dispose();
            }
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
                    builder.AddService<Services.EchoService>();
                    builder.AddServiceEndpoint<Services.EchoService, ServiceContract.IEchoService>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
                });
            }
        }
    }
}
