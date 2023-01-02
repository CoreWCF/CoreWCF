// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Net.Http.Headers;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Http.Tests.Authorization
{
    public partial class AuthorizationIntegrationTests
    {


        private readonly ITestOutputHelper _output;

        public AuthorizationIntegrationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        public static IEnumerable<object[]> Get_Return401_WhenUserIsNotAuthenticated_TestVariations()
        {
            yield return new object[] { new Uri("http://localhost:8080"), nameof(SecuredService.Default), new Predicate<SecuredServiceHolder>(x => x.IsDefaultCalled) };
            yield return new object[] { new Uri("http://localhost:8080"), nameof(SecuredService.Read), new Predicate<SecuredServiceHolder>(x => x.IsReadCalled) };
            yield return new object[] { new Uri("http://localhost:8080"), nameof(SecuredService.Write), new Predicate<SecuredServiceHolder>(x => x.IsWriteCalled) };
            yield return new object[] { new Uri("http://localhost:8080"), nameof(SecuredService.Generated), new Predicate<SecuredServiceHolder>(x => x.IsGeneratedCalled) };
            yield return new object[] { new Uri("https://localhost:8443"), nameof(SecuredService.Default), new Predicate<SecuredServiceHolder>(x => x.IsDefaultCalled) };
            yield return new object[] { new Uri("https://localhost:8443"), nameof(SecuredService.Read), new Predicate<SecuredServiceHolder>(x => x.IsReadCalled) };
            yield return new object[] { new Uri("https://localhost:8443"), nameof(SecuredService.Write), new Predicate<SecuredServiceHolder>(x => x.IsWriteCalled) };
            yield return new object[] { new Uri("https://localhost:8443"), nameof(SecuredService.Generated), new Predicate<SecuredServiceHolder>(x => x.IsGeneratedCalled) };
        }

        [Theory]
        [MemberData(nameof(Get_Return401_WhenUserIsNotAuthenticated_TestVariations))]
        public async Task Return401_WhenUserIsNotAuthenticated(Uri baseUri, string operationContractName, Predicate<SecuredServiceHolder> predicate)
        {
            using var factory = new AuthorizationWebApplicationFactory<Startup>();

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
            Assert.Contains(response.Headers,
                x => x.Key == HeaderNames.WWWAuthenticate && x.Value.FirstOrDefault() ==
                    FakeJwtBearerAuthenticationHandler.AuthenticationScheme);
            var authenticationServiceInterceptor = Assert.IsType<AuthenticationServiceHolder>(factory.AuthenticationServiceHolder);
            Assert.True(authenticationServiceInterceptor.IsAuthenticateAsyncCalled);
            var authorizationServiceInterceptor = Assert.IsType<AuthorizationServiceHolder>(factory.AuthorizationServiceHolder);
            Assert.False(authorizationServiceInterceptor.IsAuthorizeAsyncCalled);
            Assert.False(predicate.Invoke(factory.SecuredServiceHolder));
        }

        public static IEnumerable<object[]> Get_Return500WithAccessIdDeniedFault_WhenUserIsNotAuthorized_TestVariations()
        {
            yield return new object[] { new Uri("http://localhost:8080"), nameof(SecuredService.Read), true, DefinedScopeValues.Write, new Predicate<SecuredServiceHolder>(x => x.IsReadCalled) };
            yield return new object[] { new Uri("http://localhost:8080"), nameof(SecuredService.Read), true, null, new Predicate<SecuredServiceHolder>(x => x.IsReadCalled) };
            yield return new object[] { new Uri("http://localhost:8080"), nameof(SecuredService.Write), true, DefinedScopeValues.Read, new Predicate<SecuredServiceHolder>(x => x.IsWriteCalled) };
            yield return new object[] { new Uri("http://localhost:8080"), nameof(SecuredService.Write), true, null, new Predicate<SecuredServiceHolder>(x => x.IsWriteCalled) };
            yield return new object[] { new Uri("http://localhost:8080"), nameof(SecuredService.Generated), true, DefinedScopeValues.Read, new Predicate<SecuredServiceHolder>(x => x.IsGeneratedCalled) };
            yield return new object[] { new Uri("http://localhost:8080"), nameof(SecuredService.Generated), true, null, new Predicate<SecuredServiceHolder>(x => x.IsGeneratedCalled) };
            yield return new object[] { new Uri("https://localhost:8443"), nameof(SecuredService.Read), true, DefinedScopeValues.Write, new Predicate<SecuredServiceHolder>(x => x.IsReadCalled) };
            yield return new object[] { new Uri("https://localhost:8443"), nameof(SecuredService.Read), true, null, new Predicate<SecuredServiceHolder>(x => x.IsReadCalled) };
            yield return new object[] { new Uri("https://localhost:8443"), nameof(SecuredService.Write), true, DefinedScopeValues.Read, new Predicate<SecuredServiceHolder>(x => x.IsWriteCalled) };
            yield return new object[] { new Uri("https://localhost:8443"), nameof(SecuredService.Write), true, null, new Predicate<SecuredServiceHolder>(x => x.IsWriteCalled) };
            yield return new object[] { new Uri("https://localhost:8443"), nameof(SecuredService.Generated), true, DefinedScopeValues.Read, new Predicate<SecuredServiceHolder>(x => x.IsGeneratedCalled) };
            yield return new object[] { new Uri("https://localhost:8443"), nameof(SecuredService.Generated), true, null, new Predicate<SecuredServiceHolder>(x => x.IsGeneratedCalled) };
        }

        [Theory]
        [MemberData(nameof(Get_Return500WithAccessIdDeniedFault_WhenUserIsNotAuthorized_TestVariations))]
        public async Task Return500WithAccessIdDeniedFault_WhenUserIsNotAuthorized(Uri baseUri, string operationContractName, bool isAuthenticated,
            string scopeClaimValue, Predicate<SecuredServiceHolder> predicate)
        {
            using var factory = new AuthorizationWebApplicationFactory<Startup>();

            factory.IsAuthenticated = isAuthenticated;
            if (scopeClaimValue != null)
            {
                factory.ScopeClaimValues.Add(scopeClaimValue);
            }


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
            var authenticationServiceInterceptor = Assert.IsType<AuthenticationServiceHolder>(factory.AuthenticationServiceHolder);
            Assert.True(authenticationServiceInterceptor.IsAuthenticateAsyncCalled);
            var authorizationServiceInterceptor = Assert.IsType<AuthorizationServiceHolder>(factory.AuthorizationServiceHolder);
            Assert.True(authorizationServiceInterceptor.IsAuthorizeAsyncCalled);
            Assert.False(predicate.Invoke(factory.SecuredServiceHolder));
        }

        public static IEnumerable<object[]> Get_Return200_WhenUserMatchPolicy_TestVariations()
        {
            yield return new object[] { new Uri("http://localhost:8080"), nameof(SecuredService.Read), true, DefinedScopeValues.Read, new Predicate<SecuredServiceHolder>(x => x.IsReadCalled)  };
            yield return new object[] { new Uri("http://localhost:8080"), nameof(SecuredService.Generated), true, DefinedScopeValues.Write, new Predicate<SecuredServiceHolder>(x => x.IsGeneratedCalled) };
            yield return new object[] { new Uri("http://localhost:8080"), nameof(SecuredService.Write), true, DefinedScopeValues.Write, new Predicate<SecuredServiceHolder>(x => x.IsWriteCalled) };
            yield return new object[] { new Uri("http://localhost:8080"), nameof(SecuredService.Default), true, null, new Predicate<SecuredServiceHolder>(x => x.IsDefaultCalled) };
            yield return new object[] { new Uri("https://localhost:8443"), nameof(SecuredService.Read), true, DefinedScopeValues.Read, new Predicate<SecuredServiceHolder>(x => x.IsReadCalled) };
            yield return new object[] { new Uri("https://localhost:8443"), nameof(SecuredService.Generated), true, DefinedScopeValues.Write, new Predicate<SecuredServiceHolder>(x => x.IsGeneratedCalled) };
            yield return new object[] { new Uri("https://localhost:8443"), nameof(SecuredService.Write), true, DefinedScopeValues.Write, new Predicate<SecuredServiceHolder>(x => x.IsWriteCalled) };
            yield return new object[] { new Uri("https://localhost:8443"), nameof(SecuredService.Default), true, null, new Predicate<SecuredServiceHolder>(x => x.IsDefaultCalled) };
        }

        [Theory]
        [MemberData(nameof(Get_Return200_WhenUserMatchPolicy_TestVariations))]
        public async Task Return200_WhenUserMatchPolicy(Uri baseUri, string operationContractName, bool isAuthenticated, string scopeClaimValue, Predicate<SecuredServiceHolder> predicate)
        {
            using var factory = new AuthorizationWebApplicationFactory<Startup>();

            factory.IsAuthenticated = isAuthenticated;
            if (scopeClaimValue != null)
            {
                factory.ScopeClaimValues.Add(scopeClaimValue);
            }

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
            var authenticationServiceInterceptor = Assert.IsType<AuthenticationServiceHolder>(factory.AuthenticationServiceHolder);
            Assert.True(authenticationServiceInterceptor.IsAuthenticateAsyncCalled);
            var authorizationServiceInterceptor = Assert.IsType<AuthorizationServiceHolder>(factory.AuthorizationServiceHolder);
            Assert.True(authorizationServiceInterceptor.IsAuthorizeAsyncCalled);
            Assert.True(predicate.Invoke(factory.SecuredServiceHolder));
        }
    }
}
