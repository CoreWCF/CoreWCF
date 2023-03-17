// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using CoreWCF.Configuration;
using Helpers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests
{
    // This class is for tests to validate we don't regress on targetted fixes to behavior
    public class RegressionFixes
    {
        private readonly ITestOutputHelper _output;

        public RegressionFixes(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void HandleQuotesInContentTypeCharSet()
        {
            string testString = new string('a', 3000);
            IWebHost host = ServiceHelper.CreateWebHostBuilder<StartupCharSetQuotes>(_output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/BasicWcfService/ITestBasicScenariosService.svc")));
                ClientContract.IEchoService channel = factory.CreateChannel();
                var result = channel.EchoString(testString);
                Assert.Equal(testString, result);
            }
        }

        internal class StartupCharSetQuotes : StartupBase
        {
            public override void Configure(IApplicationBuilder app)
            {
                app.Use(async (context, next) =>
                {
                    var typedHeaders = context.Request.GetTypedHeaders();
                    var contentType = typedHeaders.ContentType;
                    contentType.Charset = '"' + contentType.Charset.ToString() + '"';
                    typedHeaders.ContentType = contentType;
                    await next.Invoke();
                });
                base.Configure(app);
            }
        }

        internal abstract class StartupBase
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
            }

            public virtual void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.EchoService>();
                    builder.AddServiceEndpoint<Services.EchoService, ServiceContract.IEchoService>(new BasicHttpBinding(), "/BasicWcfService/ITestBasicScenariosService.svc");
                });
            }
        }
    }
}
