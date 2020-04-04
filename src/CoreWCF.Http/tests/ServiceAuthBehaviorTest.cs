using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.ServiceModel.Security;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using CoreWCF.Description;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.Primitives.Tests.CustomSecurity;
using Helpers;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests
{
    public class ServiceAuthBehaviorTest
    {
        private ITestOutputHelper _output;

        public ServiceAuthBehaviorTest(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void BasicHttpRequestReplyEchoWithServiceBehavior()
        {
            string testString = new string('a', 3000);
            var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();
                var httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/basichttp.svc")));
                var channel = factory.CreateChannel();
                var result = channel.EchoString(testString);
                Assert.Equal(testString, result);
            }
        }

        [Fact]
        public void AccessDeniedForBasicHttpRequestReplyEcho()
        {
            string testString = new string('a', 3000);
            var host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();
                var httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri("http://localhost:8080/BasicWcfService/basichttp.svc")));
                var channel = factory.CreateChannel();
                SecurityAccessDeniedException ex = Assert.Throws<SecurityAccessDeniedException>(() => channel.EchoToFail(testString));
                Assert.Equal("Access is denied.", ex.Message);
            }
        }

        internal class Startup
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
                var authBehavior = new ServiceAuthorizationBehavior();
                services.AddSingleton<IServiceBehavior>(authBehavior);
                services.AddSingleton(authBehavior);
            }

            public void Configure(IApplicationBuilder app, IHostingEnvironment env)
            {
                var authBehavior = app.ApplicationServices.GetRequiredService<ServiceAuthorizationBehavior>();
                var authPolicies = new List<IAuthorizationPolicy>();
                authPolicies.Add(new MyTestAuthorizationPolicy());
                var externalAuthPolicies = new ReadOnlyCollection<IAuthorizationPolicy>(authPolicies);
                authBehavior.ExternalAuthorizationPolicies = externalAuthPolicies;
                authBehavior.ServiceAuthorizationManager = new MyTestServiceAuthorizationManager();
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.EchoService>();
                    builder.AddServiceEndpoint<Services.EchoService, ServiceContract.IEchoService>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
                });
            }
        }

    }
}

