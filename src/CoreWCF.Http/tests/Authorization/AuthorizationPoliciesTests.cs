// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using CoreWCF.Configuration;
using CoreWCF.Description;
using CoreWCF.Http.Tests.Helpers;
using CoreWCF.Security.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests.Authorization
{
    public class AuthorizationPoliciesTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly AuthNAuthZIntegrationTest<Startup> _factory;

        public AuthorizationPoliciesTests(ITestOutputHelper output)
        {
            _output = output;
            _factory = new AuthNAuthZIntegrationTest<Startup>();
        }

        [Theory]
        [InlineData(nameof(EchoService.Default))]
        [InlineData(nameof(EchoService.AdminOnly))]
        [InlineData(nameof(EchoService.Write))]
        public async Task Return401WhenUserIsNotAuthenticated(string operationContractName)
        {
            _factory.IsAuthenticated = false;

            var client = _factory.CreateClient();
            string action = $"http://tempuri.org/IEchoService/{operationContractName}";

            var request = new HttpRequestMessage(HttpMethod.Post,
                new Uri("http://localhost:8080/BasicWcfService/basichttp.svc", UriKind.Absolute));
            request.Headers.TryAddWithoutValidation("SOAPAction", $"\"{action}\"");

            string requestBody =
                @$"<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:tem=""http://tempuri.org/"">
   <s:Header/>
   <s:Body>
      <tem:{operationContractName}>
         <tem:text>A</tem:text>
      </tem:{operationContractName}>
   </s:Body>
</s:Envelope>";

            request.Content = new StringContent(requestBody, Encoding.UTF8, "text/xml");

            // FIXME: Commenting out this line will induce a chunked response, which will break the pre-read message parser
            request.Content.Headers.ContentLength = Encoding.UTF8.GetByteCount(requestBody);

            var response = await client.SendAsync(request);
            if (response.StatusCode == HttpStatusCode.InternalServerError)
            {
                _output.WriteLine(await response.Content.ReadAsStringAsync());
            }

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        public static IEnumerable<object[]> GetReturn403WhenUserIsNotAuthorizedTestVariations()
        {
            yield return new object[] { nameof(EchoService.AdminOnly), true, DefinedScopes.Write };
            yield return new object[] { nameof(EchoService.AdminOnly), true, DefinedScopes.Read };
            yield return new object[] { nameof(EchoService.Write), true, DefinedScopes.Read };
        }

        [Theory]
        [MemberData(nameof(GetReturn403WhenUserIsNotAuthorizedTestVariations))]
        public async Task Return403WhenUserIsNotAuthorized(string operationContractName, bool isAuthenticated, string scopeClaimValue)
        {
            _factory.IsAuthenticated = isAuthenticated;
            _factory.DefaultScopeClaimValue = scopeClaimValue;

            var client = _factory.CreateClient();
            string action = $"http://tempuri.org/IEchoService/{operationContractName}";

            var request = new HttpRequestMessage(HttpMethod.Post,
                new Uri("http://localhost:8080/BasicWcfService/basichttp.svc", UriKind.Absolute));
            request.Headers.TryAddWithoutValidation("SOAPAction", $"\"{action}\"");

            string requestBody =
                @$"<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:tem=""http://tempuri.org/"">
   <s:Header/>
   <s:Body>
      <tem:{operationContractName}>
         <tem:text>A</tem:text>
      </tem:{operationContractName}>
   </s:Body>
</s:Envelope>";

            request.Content = new StringContent(requestBody, Encoding.UTF8, "text/xml");

            // FIXME: Commenting out this line will induce a chunked response, which will break the pre-read message parser
            request.Content.Headers.ContentLength = Encoding.UTF8.GetByteCount(requestBody);

            var response = await client.SendAsync(request);
            if (response.StatusCode == HttpStatusCode.InternalServerError)
            {
                _output.WriteLine(await response.Content.ReadAsStringAsync());
            }

            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        public static IEnumerable<object[]> GetReturn200WhenUserMatchPolicyTestVariations()
        {
            yield return new object[] { nameof(EchoService.AdminOnly), true, DefinedScopes.Admin };
            yield return new object[] { nameof(EchoService.Write), true, DefinedScopes.Admin };
            yield return new object[] { nameof(EchoService.Default), true, DefinedScopes.Admin };
            yield return new object[] { nameof(EchoService.Anonymous), true, DefinedScopes.Admin };

            yield return new object[] { nameof(EchoService.Write), true, DefinedScopes.Write };
            yield return new object[] { nameof(EchoService.Default), true, DefinedScopes.Write };
            yield return new object[] { nameof(EchoService.Anonymous), true, DefinedScopes.Write };

            yield return new object[] { nameof(EchoService.Default), true, DefinedScopes.Read };
            yield return new object[] { nameof(EchoService.Anonymous), true, DefinedScopes.Read };

            yield return new object[] { nameof(EchoService.Anonymous), false, null };
        }

        [Theory]
        [MemberData(nameof(GetReturn200WhenUserMatchPolicyTestVariations))]
        public async Task Return200WhenUserMatchPolicy(string operationContractName, bool isAuthenticated, string scopeClaimValue)
        {
            _factory.IsAuthenticated = isAuthenticated;
            _factory.DefaultScopeClaimValue = scopeClaimValue;

            var client = _factory.CreateClient();
            string action = $"http://tempuri.org/IEchoService/{operationContractName}";

            var request = new HttpRequestMessage(HttpMethod.Post,
                new Uri("http://localhost:8080/BasicWcfService/basichttp.svc", UriKind.Absolute));
            request.Headers.TryAddWithoutValidation("SOAPAction", $"\"{action}\"");

            string requestBody =
                @$"<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"" xmlns:tem=""http://tempuri.org/"">
   <s:Header/>
   <s:Body>
      <tem:{operationContractName}>
         <tem:text>A</tem:text>
      </tem:{operationContractName}>
   </s:Body>
</s:Envelope>";

            request.Content = new StringContent(requestBody, Encoding.UTF8, "text/xml");

            // FIXME: Commenting out this line will induce a chunked response, which will break the pre-read message parser
            request.Content.Headers.ContentLength = Encoding.UTF8.GetByteCount(requestBody);

            var response = await client.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();
            _output.WriteLine(responseBody);

            string expected = "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\">" +
                                    "<s:Body>" +
                                    $"<{operationContractName}Response xmlns=\"http://tempuri.org/\">" +
                                    $"<{operationContractName}Result>A</{operationContractName}Result>" +
                                    $"</{operationContractName}Response>" +
                                    "</s:Body>" +
                                    "</s:Envelope>";

            Assert.True(response.IsSuccessStatusCode);
            Assert.Equal(expected, responseBody);
        }

        public class Startup
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
                services.AddSingleton<IServiceBehavior, AuthorizationServiceBehavior>();
            }

            public void Configure(IApplicationBuilder app)
            {
                app.UseServiceModel(builder =>
                {
                    builder.AddService<EchoService>();
                    builder.AddServiceEndpoint<EchoService, IEchoService>(
                        new BasicHttpBinding
                        {
                            Security = new BasicHttpSecurity
                            {
                                Transport = new HttpTransportSecurity
                                {
                                    ClientCredentialType = HttpClientCredentialType.Custom,
                                    CustomAuthenticationScheme = "Bearer"
                                }
                            }
                        }, "/BasicWcfService/basichttp.svc");
                });
            }
        }

        [ServiceContract]
        public interface IEchoService
        {
            [OperationContract]
            string Default(string text);
            [OperationContract]
            string AdminOnly(string text);
            [OperationContract]
            string Anonymous(string text);
            [OperationContract]
            Task<string> Write(string text);
        }

        [ServiceBehavior(IncludeExceptionDetailInFaults = true)]
        public class EchoService : IEchoService
        {
            // No attribute => defaults to the builtin default policy which is RequireAuthenticatedUser
            public string Default(string text) => text;
            [Authorize(Policy = Policies.AdminOnly)]
            public string AdminOnly(string text) => text;
            [AllowAnonymous]
            public string Anonymous(string text) => text;
            [Authorize(Policy = Policies.Write)]
            public Task<string> Write(string text) => Task.FromResult(text);
        }

        public void Dispose() => _factory?.Dispose();
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
}
