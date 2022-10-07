// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using CoreWCF.Http.Tests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests.Authorization
{
    public class AuthorizationPoliciesTests : IDisposable
    {
        private readonly ITestOutputHelper _output;
        private readonly AuthNAuthZIntegrationTest<AuthorizationStartup> _factory;

        public AuthorizationPoliciesTests(ITestOutputHelper output)
        {
            _output = output;
            _factory = new AuthNAuthZIntegrationTest<AuthorizationStartup>();
        }

        [Theory]
        [InlineData(nameof(AuthorizationStartup.SecuredService.Default))]
        [InlineData(nameof(AuthorizationStartup.SecuredService.AdminOnly))]
        [InlineData(nameof(AuthorizationStartup.SecuredService.Write))]
        public async Task Return401WhenUserIsNotAuthenticated(string operationContractName)
        {
            _factory.IsAuthenticated = false;

            var client = _factory.CreateClient();
            string action = $"http://tempuri.org/ISecuredService/{operationContractName}";

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

        public static IEnumerable<object[]> Get_Return500WithAccessIdDeniedFault_WhenUserIsNotAuthorized_TestVariations()
        {
            yield return new object[] { nameof(AuthorizationStartup.SecuredService.AdminOnly), true,AuthorizationStartup. DefinedScopes.Write };
            yield return new object[] { nameof(AuthorizationStartup.SecuredService.AdminOnly), true, AuthorizationStartup.DefinedScopes.Read };
            yield return new object[] { nameof(AuthorizationStartup.SecuredService.Write), true, AuthorizationStartup.DefinedScopes.Read };
        }

        [Theory]
        [MemberData(nameof(Get_Return500WithAccessIdDeniedFault_WhenUserIsNotAuthorized_TestVariations))]
        public async Task Return500WithAccessIdDeniedFault_WhenUserIsNotAuthorized(string operationContractName, bool isAuthenticated,
            string scopeClaimValue)
        {
            _factory.IsAuthenticated = isAuthenticated;
            _factory.DefaultScopeClaimValue = scopeClaimValue;

            var client = _factory.CreateClient();
            string action = $"http://tempuri.org/ISecuredService/{operationContractName}";

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
            var responseContent = await response.Content.ReadAsStringAsync();
            _output.WriteLine(responseContent);
            Assert.False(response.IsSuccessStatusCode);
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Contains("Access is denied", responseContent);
        }

        public static IEnumerable<object[]> Get_Return200_WhenUserMatchPolicy_TestVariations()
        {
            yield return new object[] { nameof(AuthorizationStartup.SecuredService.AdminOnly), true, AuthorizationStartup.DefinedScopes.Admin };
            yield return new object[] { nameof(AuthorizationStartup.SecuredService.Write), true, AuthorizationStartup.DefinedScopes.Admin };
            yield return new object[] { nameof(AuthorizationStartup.SecuredService.Default), true, AuthorizationStartup.DefinedScopes.Admin };

            yield return new object[] { nameof(AuthorizationStartup.SecuredService.Write), true, AuthorizationStartup.DefinedScopes.Write };
            yield return new object[] { nameof(AuthorizationStartup.SecuredService.Default), true, AuthorizationStartup.DefinedScopes.Write };

            yield return new object[] { nameof(AuthorizationStartup.SecuredService.Default), true, AuthorizationStartup.DefinedScopes.Read };
        }

        [Theory]
        [MemberData(nameof(Get_Return200_WhenUserMatchPolicy_TestVariations))]
        public async Task Return200_WhenUserMatchPolicy(string operationContractName, bool isAuthenticated,
            string scopeClaimValue)
        {
            _factory.IsAuthenticated = isAuthenticated;
            _factory.DefaultScopeClaimValue = scopeClaimValue;

            var client = _factory.CreateClient();
            string action = $"http://tempuri.org/ISecuredService/{operationContractName}";

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

        public void Dispose() => _factory?.Dispose();
    }
}
