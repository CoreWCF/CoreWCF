// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests.Authorization
{
    public class AuthorizationPoliciesTests
    {
        private readonly ITestOutputHelper _output;

        public AuthorizationPoliciesTests(ITestOutputHelper output)
        {
            _output = output;
        }

        public static IEnumerable<object[]> Get_Return401_WhenUserIsNotAuthenticated_TestVariations()
        {
            yield return new object[] { new Uri("http://localhost:8080"), nameof(SecuredService.Default) };
            yield return new object[] { new Uri("http://localhost:8080"), nameof(SecuredService.AdminOnly) };
            yield return new object[] { new Uri("http://localhost:8080"), nameof(SecuredService.Write) };

            yield return new object[] { new Uri("https://localhost:8443"), nameof(SecuredService.Default) };
            yield return new object[] { new Uri("https://localhost:8443"), nameof(SecuredService.AdminOnly) };
            yield return new object[] { new Uri("https://localhost:8443"), nameof(SecuredService.Write) };
        }

        [Theory]
        [MemberData(nameof(Get_Return401_WhenUserIsNotAuthenticated_TestVariations))]
        public async Task Return401_WhenUserIsNotAuthenticated(Uri baseUri, string operationContractName)
        {
            using var factory = new AuthorizationIntegrationTest<AuthorizationStartup>();

            factory.IsAuthenticated = false;

            var client = factory.CreateClient();
            string action = $"http://tempuri.org/ISecuredService/{operationContractName}";

            var request = new HttpRequestMessage(HttpMethod.Post, new Uri(baseUri, "/BasicWcfService/basichttp.svc"));
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
            yield return new object[] { new Uri("http://localhost:8080"), nameof(SecuredService.AdminOnly), true, DefinedScopes.Write };
            yield return new object[] { new Uri("http://localhost:8080"), nameof(SecuredService.AdminOnly), true, DefinedScopes.Read };
            yield return new object[] { new Uri("http://localhost:8080"), nameof(SecuredService.Write), true, DefinedScopes.Read };
            yield return new object[] { new Uri("http://localhost:8080"), nameof(SecuredService.Generated), true, DefinedScopes.Write };
            yield return new object[] { new Uri("http://localhost:8080"), nameof(SecuredService.Generated), true, DefinedScopes.Read };

            yield return new object[] { new Uri("https://localhost:8443"), nameof(SecuredService.AdminOnly), true, DefinedScopes.Write };
            yield return new object[] { new Uri("https://localhost:8443"), nameof(SecuredService.AdminOnly), true, DefinedScopes.Read };
            yield return new object[] { new Uri("https://localhost:8443"), nameof(SecuredService.Write), true, DefinedScopes.Read };
            yield return new object[] { new Uri("https://localhost:8443"), nameof(SecuredService.Generated), true, DefinedScopes.Write };
            yield return new object[] { new Uri("https://localhost:8443"), nameof(SecuredService.Generated), true, DefinedScopes.Read };
        }

        [Theory]
        [MemberData(nameof(Get_Return500WithAccessIdDeniedFault_WhenUserIsNotAuthorized_TestVariations))]
        public async Task Return500WithAccessIdDeniedFault_WhenUserIsNotAuthorized(Uri baseUri, string operationContractName, bool isAuthenticated,
            string scopeClaimValue)
        {
            using var factory = new AuthorizationIntegrationTest<AuthorizationStartup>();

            factory.IsAuthenticated = isAuthenticated;
            factory.DefaultScopeClaimValue = scopeClaimValue;

            var client = factory.CreateClient();
            string action = $"http://tempuri.org/ISecuredService/{operationContractName}";

            var request = new HttpRequestMessage(HttpMethod.Post, new Uri(baseUri, "/BasicWcfService/basichttp.svc"));
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
            yield return new object[] { new Uri("http://localhost:8080"), nameof(SecuredService.AdminOnly), true, DefinedScopes.Admin };
            yield return new object[] { new Uri("http://localhost:8080"), nameof(SecuredService.Write), true, DefinedScopes.Admin };
            yield return new object[] { new Uri("http://localhost:8080"), nameof(SecuredService.Default), true, DefinedScopes.Admin };
            yield return new object[] { new Uri("http://localhost:8080"), nameof(SecuredService.Write), true, DefinedScopes.Write };
            yield return new object[] { new Uri("http://localhost:8080"), nameof(SecuredService.Default), true, DefinedScopes.Write };
            yield return new object[] { new Uri("http://localhost:8080"), nameof(SecuredService.Default), true, DefinedScopes.Read };

            yield return new object[] { new Uri("https://localhost:8443"), nameof(SecuredService.AdminOnly), true, DefinedScopes.Admin };
            yield return new object[] { new Uri("https://localhost:8443"), nameof(SecuredService.Write), true, DefinedScopes.Admin };
            yield return new object[] { new Uri("https://localhost:8443"), nameof(SecuredService.Default), true, DefinedScopes.Admin };
            yield return new object[] { new Uri("https://localhost:8443"), nameof(SecuredService.Write), true, DefinedScopes.Write };
            yield return new object[] { new Uri("https://localhost:8443"), nameof(SecuredService.Default), true, DefinedScopes.Write };
            yield return new object[] { new Uri("https://localhost:8443"), nameof(SecuredService.Default), true, DefinedScopes.Read };
        }

        [Theory]
        [MemberData(nameof(Get_Return200_WhenUserMatchPolicy_TestVariations))]
        public async Task Return200_WhenUserMatchPolicy(Uri baseUri, string operationContractName, bool isAuthenticated, string scopeClaimValue)
        {
            using var factory = new AuthorizationIntegrationTest<AuthorizationStartup>();

            factory.IsAuthenticated = isAuthenticated;
            factory.DefaultScopeClaimValue = scopeClaimValue;

            var client = factory.CreateClient();
            string action = $"http://tempuri.org/ISecuredService/{operationContractName}";

            var request = new HttpRequestMessage(HttpMethod.Post, new Uri(baseUri, "/BasicWcfService/basichttp.svc"));
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
    }
}
