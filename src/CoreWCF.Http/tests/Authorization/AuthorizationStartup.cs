﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace CoreWCF.Http.Tests.Authorization;

public class AuthorizationStartup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            options.AddPolicy(Policies.Write,
                policy => policy.RequireClaim("scope", new[] { DefinedScopes.Write, DefinedScopes.Admin }));
            options.AddPolicy(Policies.AdminOnly,
                policy => policy.RequireClaim("scope", new[] { DefinedScopes.Admin }));
        });
        services.AddServiceModelServices();
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseServiceModel(builder =>
        {
            builder.AddService<SecuredService>();
            builder.AddServiceEndpoint<SecuredService, ISecuredService>(new BasicHttpBinding
            {
                Security = new BasicHttpSecurity
                {
                    Mode = BasicHttpSecurityMode.None,
                    Transport = new HttpTransportSecurity
                    {
                        ClientCredentialType = HttpClientCredentialType.InheritedFromHost
                    }
                }
            }, "/BasicWcfService/basichttp.svc");
            builder.AddServiceEndpoint<SecuredService, ISecuredService>(new BasicHttpBinding
            {
                Security = new BasicHttpSecurity
                {
                    Mode = BasicHttpSecurityMode.Transport,
                    Transport = new HttpTransportSecurity
                    {
                        ClientCredentialType = HttpClientCredentialType.InheritedFromHost
                    }
                }
            }, "/BasicWcfService/basichttp.svc");

        });
    }
}

[ServiceContract]
public interface ISecuredService
{
    [OperationContract]
    string Default(string text);

    [OperationContract]
    string AdminOnly(string text);

    [OperationContract]
    Task<string> Write(string text);

    [OperationContract]
    Task<string> Generated(string text);
}

public partial class SecuredService
{
    public Task<string> Generated(string text) => Task.FromResult(text);
}

[ServiceBehavior(IncludeExceptionDetailInFaults = true)]
public partial class SecuredService : ISecuredService
{
    // No attribute => defaults to the builtin default policy which is RequireAuthenticatedUser
    public string Default(string text) => text;

    [Authorize(Policy = Policies.AdminOnly)]
    public string AdminOnly(string text) => text;

    [Authorize(Policy = Policies.Write)]
    public Task<string> Write(string text) => Task.FromResult(text);

    [Authorize(Policy = Policies.AdminOnly)]
    public Task<string> Generated(string text, [Injected] HttpContext httpContext) => Task.FromResult(text);
}

internal static class Policies
{
    public const string AdminOnly = nameof(AdminOnly);
    public const string Write = nameof(Write);
}

internal static class DefinedScopes
{
    public const string Admin = nameof(Admin);
    public const string Read = nameof(Read);
    public const string Write = nameof(Write);
}