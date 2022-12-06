// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using CoreWCF.Web;
using Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.WebHttp.Tests.Authorization;

public class AuthorizationTests
{
    private readonly ITestOutputHelper _output;
    private const string TestString = nameof(TestString);

    public AuthorizationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task SinglePolicy_AuthenticatedUser_HavingRequiredScopeValues_Test()
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder<SinglePolicyOnOperationContractWithAuthenticatedUserAndRequiredScopeValuesStartup>(_output).Build();
        using (host)
        {
            await host.StartAsync();
            (HttpStatusCode statusCode, string content) = await HttpHelpers.PostJsonAsync("api/echo", TestString);
            Assert.Equal(HttpStatusCode.OK, statusCode);
            Assert.Equal(JsonConvert.SerializeObject(TestString), content);
        }
    }

    [Fact]
    public async Task SinglePolicy_UnauthenticatedUser_Test()
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder<SinglePolicyOnOperationContractWithUnauthenticatedUserStartup>(_output).Build();
        using (host)
        {
            await host.StartAsync();
            (HttpStatusCode statusCode, string content) = await HttpHelpers.PostJsonAsync("api/echo", TestString);
            Assert.Equal(HttpStatusCode.Unauthorized, statusCode);
        }
    }

    [Fact]
    public async Task SinglePolicy_AuthenticatedUser_MissingScopeValues_Test()
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder<SinglePolicyOnOperationContractWithAuthenticatedUserButMissingScopeValuesStartup>(_output).Build();
        using (host)
        {
            await host.StartAsync();
            (HttpStatusCode statusCode, string content) = await HttpHelpers.PostJsonAsync("api/echo", TestString);
            Assert.Equal(HttpStatusCode.BadRequest, statusCode);
            Assert.Contains("Access is denied", content);
        }
    }

    private static class Policies
    {
        public const string Read = nameof(Read);
        public const string Write = nameof(Write);
    }

    private static class DefinedScopeValues
    {
        public const string Read = nameof(Read);
        public const string Write = nameof(Write);
    }

    private abstract class Startup<TSecuredService> where TSecuredService : class, ISecuredService
    {
        public bool IsAuthenticated { get; set; }
        public List<string> ScopeClaimValues { get; set; } = new();

        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<FakeJwtBearerAuthenticationHandlerOptions>(options =>
            {
                options.IsAuthenticated = IsAuthenticated;
                options.ScopeClaimValues = ScopeClaimValues;
            });

            services.AddAuthentication(FakeJwtBearerAuthenticationHandler.AuthenticationScheme)
                .AddScheme<FakeJwtBearerAuthenticationHandlerOptions, FakeJwtBearerAuthenticationHandler>(FakeJwtBearerAuthenticationHandler.AuthenticationScheme,
                    options =>
                    {
                        options.IsAuthenticated = IsAuthenticated;
                        options.ScopeClaimValues = ScopeClaimValues;
                    });

            services.AddAuthorization(options =>
            {
                options.AddPolicy(Policies.Write,
                    policy => policy.RequireAuthenticatedUser().RequireClaim("scope", DefinedScopeValues.Write));
                options.AddPolicy(Policies.Read,
                    policy => policy.RequireAuthenticatedUser().RequireClaim("scope", DefinedScopeValues.Read));
            });
            services.AddServiceModelWebServices();
            services.AddHttpContextAccessor();
            if (typeof(TSecuredService).IsInterface)
            {
                services.AddTransient<ISecuredService, TSecuredService>();
            }
            else
            {
                services.AddTransient<TSecuredService>();
            }
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseServiceModel(builder =>
            {
                builder.AddService<TSecuredService>();
                builder.AddServiceWebEndpoint<TSecuredService, ISecuredService>(
                    new WebHttpBinding
                    {
                        Security = new WebHttpSecurity
                        {
                            Mode = WebHttpSecurityMode.TransportCredentialOnly,
                            Transport = new HttpTransportSecurity
                            {
                                ClientCredentialType = HttpClientCredentialType.InheritedFromHost
                            }
                        }
                    }, "api");
            });
        }
    }


    [ServiceContract]
    internal interface ISecuredService
    {
        [WebInvoke(UriTemplate = "/echo", BodyStyle = WebMessageBodyStyle.Bare, ResponseFormat = WebMessageFormat.Json, RequestFormat = WebMessageFormat.Json)]
        string Echo(string text);
    }

    [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
    private class SinglePolicyOnOperationContractSecuredService : ISecuredService
    {
        [Authorize(Policy = Policies.Read)]
        public string Echo(string text) => text;
    }

    private class SinglePolicyOnOperationContractWithAuthenticatedUserAndRequiredScopeValuesStartup : Startup<SinglePolicyOnOperationContractSecuredService>
    {
        public SinglePolicyOnOperationContractWithAuthenticatedUserAndRequiredScopeValuesStartup()
        {
            IsAuthenticated = true;
            ScopeClaimValues.Add(DefinedScopeValues.Read);
        }
    }

    private class SinglePolicyOnOperationContractWithUnauthenticatedUserStartup : Startup<SinglePolicyOnOperationContractSecuredService>
    {
        public SinglePolicyOnOperationContractWithUnauthenticatedUserStartup()
        {
            IsAuthenticated = false;
            ScopeClaimValues.Clear();
        }
    }

    private class SinglePolicyOnOperationContractWithAuthenticatedUserButMissingScopeValuesStartup : Startup<SinglePolicyOnOperationContractSecuredService>
    {
        public SinglePolicyOnOperationContractWithAuthenticatedUserButMissingScopeValuesStartup()
        {
            IsAuthenticated = true;
            ScopeClaimValues.Clear();
        }
    }
}
