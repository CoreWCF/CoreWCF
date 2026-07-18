// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Security.Claims;
using System.ServiceModel.Security;
using CoreWCF.Configuration;
using CoreWCF.Http.Tests.Authorization.Utils;
using CoreWCF.Http.Tests.Helpers;
using Helpers;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ServiceContract;
using Xunit;

namespace CoreWCF.Http.Tests.Authorization;

/// <summary>
/// Tests that ASP.NET Core policy-based authorization with [Authorize] attributes works
/// even when using a BasicHttpBinding without InheritedFromHost/AlwaysUseAuthorizationPolicySupport.
/// This covers the scenario from https://github.com/CoreWCF/CoreWCF/issues/1408 where
/// claims are ignored when using BasicHttpBinding with Transport security.
/// </summary>
public class BasicHttpWithNoSecurityAuthorizationTests
{
    private readonly ITestOutputHelper _output;
    private const string TestString = nameof(TestString);

    public BasicHttpWithNoSecurityAuthorizationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void BasicHttpBinding_AuthenticatedUser_HavingRequiredClaim_Succeeds()
    {
        IWebHost host = ServiceHelper
            .CreateWebHostBuilder<BasicHttpWithAuthenticatedUserAndRequiredClaimStartup>(_output).Build();
        using (host)
        {
            host.Start();
            System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
            System.ServiceModel.ChannelFactory<ISecuredService> factory = new System.ServiceModel.ChannelFactory<ISecuredService>(httpBinding,
                new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/service.svc")));
            ISecuredService channel = factory.CreateChannel();
            string result = channel.Echo(TestString);
            Assert.Equal(TestString, result);
        }
    }

    [Fact]
    public void BasicHttpBinding_AuthenticatedUser_MissingRequiredClaim_IsAccessDenied()
    {
        IWebHost host = ServiceHelper
            .CreateWebHostBuilder<BasicHttpWithAuthenticatedUserButMissingClaimStartup>(_output).Build();
        using (host)
        {
            host.Start();
            System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
            System.ServiceModel.ChannelFactory<ISecuredService> factory = new System.ServiceModel.ChannelFactory<ISecuredService>(httpBinding,
                new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/service.svc")));
            ISecuredService channel = factory.CreateChannel();
            Assert.Throws<SecurityAccessDeniedException>(() => channel.Echo(TestString));
        }
    }

    [Fact]
    public void BasicHttpBinding_UnauthenticatedUser_WithAuthorizeAttribute_IsAccessDenied()
    {
        IWebHost host = ServiceHelper
            .CreateWebHostBuilder<BasicHttpWithUnauthenticatedUserStartup>(_output).Build();
        using (host)
        {
            host.Start();
            System.ServiceModel.BasicHttpBinding httpBinding = ClientHelper.GetBufferedModeBinding();
            System.ServiceModel.ChannelFactory<ISecuredService> factory = new System.ServiceModel.ChannelFactory<ISecuredService>(httpBinding,
                new System.ServiceModel.EndpointAddress(new Uri($"http://localhost:{host.GetHttpPort()}/service.svc")));
            ISecuredService channel = factory.CreateChannel();
            Assert.Throws<SecurityAccessDeniedException>(() => channel.Echo(TestString));
        }
    }

    private class BasicHttpWithAuthenticatedUserAndRequiredClaimStartup : BasicHttpNoSecurityAuthZStartupBase
    {
        public BasicHttpWithAuthenticatedUserAndRequiredClaimStartup()
        {
            IsAuthenticated = true;
            ScopeClaimValue = AuthorizationUtils.DefinedScopeValues.Read;
        }
    }

    private class BasicHttpWithAuthenticatedUserButMissingClaimStartup : BasicHttpNoSecurityAuthZStartupBase
    {
        public BasicHttpWithAuthenticatedUserButMissingClaimStartup()
        {
            IsAuthenticated = true;
            ScopeClaimValue = null; // missing claim
        }
    }

    private class BasicHttpWithUnauthenticatedUserStartup : BasicHttpNoSecurityAuthZStartupBase
    {
        public BasicHttpWithUnauthenticatedUserStartup()
        {
            IsAuthenticated = false;
            ScopeClaimValue = null;
        }
    }

    /// <summary>
    /// Startup that uses BasicHttpBinding with BasicHttpSecurityMode.None (no transport security,
    /// no InheritedFromHost), relying on UseAuthentication() in the pipeline and [Authorize] attributes
    /// on service methods. This reproduces the scenario from issue #1408.
    /// </summary>
    private abstract class BasicHttpNoSecurityAuthZStartupBase
    {
        protected bool IsAuthenticated { get; set; }
        protected string ScopeClaimValue { get; set; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
            services.AddAuthorization(options =>
            {
                options.AddPolicy(AuthorizationUtils.Policies.Read,
                    policy => policy.RequireAuthenticatedUser()
                        .RequireClaim("scope", AuthorizationUtils.DefinedScopeValues.Read));
            });
            services.AddServiceModelServices();
            services.AddSingleton<TestAuthState>(new TestAuthState { IsAuthenticated = IsAuthenticated, ScopeClaimValue = ScopeClaimValue });
            services.AddTransient<Services.SinglePolicyOnMethodSecuredService>();
        }

        public void Configure(IApplicationBuilder app)
        {
            // UseAuthentication is called globally here (not inside CoreWCF's per-endpoint pipeline)
            app.UseAuthentication();
            app.UseServiceModel(builder =>
            {
                builder.AddService<Services.SinglePolicyOnMethodSecuredService>();
                // Plain BasicHttpBinding - no InheritedFromHost, no AlwaysUseAuthorizationPolicySupport
                builder.AddServiceEndpoint<Services.SinglePolicyOnMethodSecuredService, ISecuredService>(
                    new BasicHttpBinding(), "/service.svc");
            });
        }
    }

    internal class TestAuthState
    {
        public bool IsAuthenticated { get; set; }
        public string ScopeClaimValue { get; set; }
    }

    /// <summary>
    /// A simple test authentication handler that authenticates/fails based on TestAuthState.
    /// This simulates a JWT ****** similar token-based authentication scheme that is
    /// configured globally in the ASP.NET Core pipeline.
    /// </summary>
    internal class TestAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public const string SchemeName = "TestAuth";

        private readonly TestAuthState _state;

        public TestAuthHandler(
            TestAuthState state,
            Microsoft.Extensions.Options.IOptionsMonitor<AuthenticationSchemeOptions> options,
            Microsoft.Extensions.Logging.ILoggerFactory logger,
            System.Text.Encodings.Web.UrlEncoder encoder,
            ISystemClock clock) : base(options, logger, encoder, clock)
        {
            _state = state;
        }

        protected override System.Threading.Tasks.Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!_state.IsAuthenticated)
            {
                return System.Threading.Tasks.Task.FromResult(AuthenticateResult.Fail("Not authenticated"));
            }

            var claims = new System.Collections.Generic.List<Claim>
            {
                new Claim(ClaimTypes.Name, "testuser")
            };

            if (_state.ScopeClaimValue != null)
            {
                claims.Add(new Claim("scope", _state.ScopeClaimValue));
            }

            ClaimsIdentity identity = new ClaimsIdentity(claims, SchemeName);
            ClaimsPrincipal principal = new ClaimsPrincipal(identity);
            AuthenticationTicket ticket = new AuthenticationTicket(principal, SchemeName);
            return System.Threading.Tasks.Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
