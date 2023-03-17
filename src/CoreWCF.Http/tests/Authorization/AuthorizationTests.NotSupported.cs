// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.ServiceModel.Security;
using System.Threading.Tasks;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Description;
using CoreWCF.IdentityModel.Claims;
using CoreWCF.IdentityModel.Policy;
using Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests.Authorization;

public partial class AuthorizationTests
{

    public static IEnumerable<object[]> Get_Authorization_Features_Are_Mutually_Exclusive_TestVariations()
    {
        yield return new object[]
        {
            typeof(ThrowingStartupWithServiceAuthorizationManager<SinglePolicyOnOperationContractSecuredService>)
        };
        yield return new object[]
        {
            typeof(ThrowingStartupWithAuthorizationPolicies<SinglePolicyOnOperationContractSecuredService>)
        };
    }

    [Theory]
    [MemberData(nameof(Get_Authorization_Features_Are_Mutually_Exclusive_TestVariations))]
    public void Authorization_Features_Are_Mutually_Exclusive_Test(Type startupType)
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder(_output, startupType).Build();
        var exception = Assert.Throws<NotSupportedException>(() =>
        {
            using (host)
            {
                host.Start();
            }
        });
        Assert.StartsWith("Invalid configuration.", exception.Message);
    }

    [Fact]
    public void Authorization_Features_Missing_AuthorizationService_Test()
    {
        IWebHost host = ServiceHelper.CreateWebHostBuilder<ThrowingStartupMissingAuthorizationService<SinglePolicyOnOperationContractSecuredService>>(_output).Build();
        var exception = Assert.Throws<NotSupportedException>(() =>
        {
            using (host)
            {
                host.Start();
            }
        });
        Assert.StartsWith("Invalid configuration.", exception.Message);
    }

    private class ThrowingStartupWithServiceAuthorizationManager<TSecuredService> where TSecuredService : class, ISecuredService
    {
        private class NoOpAuthorizationManager : ServiceAuthorizationManager { }

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
                .AddScheme<FakeJwtBearerAuthenticationHandlerOptions, FakeJwtBearerAuthenticationHandler>(
                    FakeJwtBearerAuthenticationHandler.AuthenticationScheme,
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
                options.DefaultPolicy =
                    new AuthorizationPolicyBuilder(FakeJwtBearerAuthenticationHandler.AuthenticationScheme)
                        .RequireClaim("scope", DefinedScopeValues.Read)
                        .Build();

            });
            services.AddServiceModelServices();
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
            ServiceAuthorizationBehavior authBehavior = app.ApplicationServices.GetRequiredService<ServiceAuthorizationBehavior>();
            authBehavior.ServiceAuthorizationManager = new NoOpAuthorizationManager();
            app.UseServiceModel(builder =>
            {
                builder.AddService<TSecuredService>();
                builder.AddServiceEndpoint<TSecuredService, ISecuredService>(
                    new BasicHttpBinding
                    {
                        Security = new BasicHttpSecurity
                        {
                            Mode = BasicHttpSecurityMode.TransportCredentialOnly,
                            Transport = new HttpTransportSecurity
                            {
                                ClientCredentialType = HttpClientCredentialType.InheritedFromHost
                            }
                        }
                    }, "/BasicWcfService/basichttp.svc");
            });
        }
    }

    private class ThrowingStartupWithAuthorizationPolicies<TSecuredService> where TSecuredService : class, ISecuredService
    {
        private class NoOpAuthorizationPolicy : IAuthorizationPolicy
        {
            public string Id { get; }
            public ClaimSet Issuer { get; }
            public bool Evaluate(EvaluationContext evaluationContext, ref object state) => true;
        }
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
                .AddScheme<FakeJwtBearerAuthenticationHandlerOptions, FakeJwtBearerAuthenticationHandler>(
                    FakeJwtBearerAuthenticationHandler.AuthenticationScheme,
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
                options.DefaultPolicy =
                    new AuthorizationPolicyBuilder(FakeJwtBearerAuthenticationHandler.AuthenticationScheme)
                        .RequireClaim("scope", DefinedScopeValues.Read)
                        .Build();

            });
            services.AddServiceModelServices();
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
            ServiceAuthorizationBehavior authBehavior = app.ApplicationServices.GetRequiredService<ServiceAuthorizationBehavior>();
            authBehavior.ExternalAuthorizationPolicies =
                new ReadOnlyCollection<IAuthorizationPolicy>(
                    new List<IAuthorizationPolicy>() { new NoOpAuthorizationPolicy() });
            app.UseServiceModel(builder =>
            {
                builder.AddService<TSecuredService>();
                builder.AddServiceEndpoint<TSecuredService, ISecuredService>(
                    new BasicHttpBinding
                    {
                        Security = new BasicHttpSecurity
                        {
                            Mode = BasicHttpSecurityMode.TransportCredentialOnly,
                            Transport = new HttpTransportSecurity
                            {
                                ClientCredentialType = HttpClientCredentialType.InheritedFromHost
                            }
                        }
                    }, "/BasicWcfService/basichttp.svc");
            });
        }
    }

    private class ThrowingStartupMissingAuthorizationService<TSecuredService> where TSecuredService : class, ISecuredService
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
                .AddScheme<FakeJwtBearerAuthenticationHandlerOptions, FakeJwtBearerAuthenticationHandler>(
                    FakeJwtBearerAuthenticationHandler.AuthenticationScheme,
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
                options.DefaultPolicy =
                    new AuthorizationPolicyBuilder(FakeJwtBearerAuthenticationHandler.AuthenticationScheme)
                        .RequireClaim("scope", DefinedScopeValues.Read)
                        .Build();

            });
            services.AddServiceModelServices();
            services.AddHttpContextAccessor();
            if (typeof(TSecuredService).IsInterface)
            {
                services.AddTransient<ISecuredService, TSecuredService>();
            }
            else
            {
                services.AddTransient<TSecuredService>();
            }

            var descriptor = services.Single(x => x.ServiceType == typeof(IAuthorizationService));
            services.Remove(descriptor);
        }

        public void Configure(IApplicationBuilder app)
        {
            app.UseServiceModel(builder =>
            {
                builder.AddService<TSecuredService>();
                builder.AddServiceEndpoint<TSecuredService, ISecuredService>(
                    new BasicHttpBinding
                    {
                        Security = new BasicHttpSecurity
                        {
                            Mode = BasicHttpSecurityMode.TransportCredentialOnly,
                            Transport = new HttpTransportSecurity
                            {
                                ClientCredentialType = HttpClientCredentialType.InheritedFromHost
                            }
                        }
                    }, "/BasicWcfService/basichttp.svc");
            });
        }
    }
}
