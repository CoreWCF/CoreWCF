// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Security.Claims;
using System.ServiceModel.Security;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using CoreWCF.Description;
using CoreWCF.IdentityModel.Policy;
using CoreWCF.Primitives.Tests.CustomSecurity;
using Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests
{
    [System.ServiceModel.ServiceContract]
    public interface IUserContextService
    {
        [System.ServiceModel.OperationContract]
        public string GetContextUser();
    }

    public partial class UserContextService : IUserContextService
    {
        public string GetContextUser([Injected] Microsoft.AspNetCore.Http.HttpContext context)
        {
            return context.User.Identity.Name;
        }
    }

    public class ServiceAuthBehaviorTest
    {
        private readonly ITestOutputHelper _output;
        public ServiceAuthBehaviorTest(ITestOutputHelper output)
        {
            _output = output;
        }

        public static IEnumerable<object[]> GetTestVariations()
        {
            yield return new object[] { typeof(Startup<MySyncTestServiceAuthorizationManager>) };
            yield return new object[] { typeof(Startup<MyAsyncTestServiceAuthorizationManager>) };
        }

        [Theory]
        [MemberData(nameof(GetTestVariations))]
        public void BasicHttpRequestReplyEchoWithServiceBehavior(Type type)
        {
            string testString = new string('a', 3000);
            IWebHost host = ServiceHelper.CreateWebHostBuilder(_output, type).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/BasicWcfService/basichttp.svc")));
                ClientContract.IEchoService channel = factory.CreateChannel();
                string result = channel.EchoString(testString);
                Assert.Equal(testString, result);
            }
        }

        [Theory]
        [MemberData(nameof(GetTestVariations))]
        public void AccessDeniedForBasicHttpRequestReplyEcho(Type type)
        {
            string testString = new string('a', 3000);
            IWebHost host = ServiceHelper.CreateWebHostBuilder(_output, type).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
                var factory = new System.ServiceModel.ChannelFactory<ClientContract.IEchoService>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/BasicWcfService/basichttp.svc")));
                ClientContract.IEchoService channel = factory.CreateChannel();
                SecurityAccessDeniedException ex = Assert.Throws<SecurityAccessDeniedException>(() => channel.EchoToFail(testString));
                Assert.Equal("Access is denied.", ex.Message);
            }
        }

        [Fact]
        public void Test_HandleRequest_SetsContextUser_WithAuthenticateAsync()
        {
            string testUser = "TestUser";
            IWebHost host = ServiceHelper.CreateWebHostBuilder<StartupWithAuthenticationScheme>(_output).Build();
            using (host)
            {
                host.Start();
                System.ServiceModel.BasicHttpBinding httpBinding = new System.ServiceModel.BasicHttpBinding(System.ServiceModel.BasicHttpSecurityMode.TransportCredentialOnly);
                httpBinding.Security.Transport.ClientCredentialType = System.ServiceModel.HttpClientCredentialType.Digest;

                var factory = new System.ServiceModel.ChannelFactory<IUserContextService>(httpBinding,
                    new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/BasicWcfService/basichttp.svc")));

                IUserContextService serviceProxy = factory.CreateChannel();
                // *** EXECUTE *** \\
                string result;
                using (var scope = new System.ServiceModel.OperationContextScope((System.ServiceModel.IContextChannel)serviceProxy))
                {
                    System.ServiceModel.Channels.HttpRequestMessageProperty requestMessageProperty;
                    if (!System.ServiceModel.OperationContext.Current.OutgoingMessageProperties.ContainsKey(System.ServiceModel.Channels.HttpRequestMessageProperty.Name))
                    {
                        requestMessageProperty = new System.ServiceModel.Channels.HttpRequestMessageProperty();
                        System.ServiceModel.OperationContext.Current.OutgoingMessageProperties[System.ServiceModel.Channels.HttpRequestMessageProperty.Name] = requestMessageProperty;
                    }
                    else
                    {
                        requestMessageProperty = (System.ServiceModel.Channels.HttpRequestMessageProperty)System.ServiceModel.OperationContext.Current.OutgoingMessageProperties[System.ServiceModel.Channels.HttpRequestMessageProperty.Name];
                    }

                    result = serviceProxy.GetContextUser();
                    Assert.Equal(testUser, result);
                }
            }
        }

        internal class Startup<TServiceAuthorizationManager> where TServiceAuthorizationManager : ServiceAuthorizationManager
        {
            public void ConfigureServices(IServiceCollection services)
            {
                services.AddServiceModelServices();
                services.AddSingleton<ServiceAuthorizationManager, TServiceAuthorizationManager>();
            }

            public void Configure(IApplicationBuilder app)
            {
                ServiceAuthorizationBehavior authBehavior = app.ApplicationServices.GetRequiredService<ServiceAuthorizationBehavior>();
                var authPolicies = new List<IAuthorizationPolicy>
                {
                    new MyTestAuthorizationPolicy()
                };
                var externalAuthPolicies = new ReadOnlyCollection<IAuthorizationPolicy>(authPolicies);
                authBehavior.ExternalAuthorizationPolicies = externalAuthPolicies;
                authBehavior.PrincipalPermissionMode = PrincipalPermissionMode.None;
                app.UseServiceModel(builder =>
                {
                    builder.AddService<Services.EchoService>();
                    builder.AddServiceEndpoint<Services.EchoService, ServiceContract.IEchoService>(new CoreWCF.BasicHttpBinding(), "/BasicWcfService/basichttp.svc");
                });
            }
        }

        public class CustomAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
        {
            [Obsolete]
            public CustomAuthenticationHandler(
                Microsoft.Extensions.Options.IOptionsMonitor<AuthenticationSchemeOptions> options,
                Microsoft.Extensions.Logging.ILoggerFactory logger,
                System.Text.Encodings.Web.UrlEncoder encoder,
                ISystemClock clock
                )
                : base(options, logger, encoder, clock)
            {
            }

            protected override Task<AuthenticateResult> HandleAuthenticateAsync()
            {
                // Custom logic to validate the user
                var claims = new[]
                {
                    new Claim(ClaimTypes.Name, "TestUser"),
                    new Claim(ClaimTypes.Role, "Admin")
                };

                var claimsIdentity = new ClaimsIdentity(claims, "Digest");
                var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);

                var ticket = new AuthenticationTicket(claimsPrincipal, "Digest");

                return Task.FromResult(AuthenticateResult.Success(ticket));
            }
        }

        internal class StartupWithAuthenticationScheme
        {
            public void ConfigureServices(IServiceCollection services)
            {
                //add two schemes to avoid the default scheme being set by AddAuthentication method
                services.AddAuthentication()
                    .AddScheme<AuthenticationSchemeOptions, CustomAuthenticationHandler>("Digest", options => { })
                    .AddScheme<AuthenticationSchemeOptions, CustomAuthenticationHandler>("Digest1", options => { });
                services.AddServiceModelServices();
                services.AddTransient<UserContextService>();
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseAuthentication();
                var binding = new CoreWCF.BasicHttpBinding
                {
                    Security = new CoreWCF.BasicHttpSecurity
                    {
                        Mode = Channels.BasicHttpSecurityMode.TransportCredentialOnly
                    }
                };
                binding.Security.Transport.ClientCredentialType = HttpClientCredentialType.Digest;

                app.UseServiceModel(builder =>
                {
                    builder.AddService<UserContextService>();
                    builder.AddServiceEndpoint<UserContextService, IUserContextService>(binding, "/BasicWcfService/basichttp.svc");
                });
            }
        }
    }
}
