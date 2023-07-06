// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using CoreWCF.Description;
using CoreWCF.Metadata.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ServiceContract;
using Services;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Metadata.Tests
{
    public class MetadataEndpointAddressServiceBehaviorTests
    {
        private readonly ITestOutputHelper _output;

        public MetadataEndpointAddressServiceBehaviorTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task XForwardedHeaders()
        {
            await TestHelper.RunSingleWsdlTestAsync<SimpleEchoService, IEchoService>(new BasicHttpBinding(),
                _output,
                configureServices: services =>
                {
                    services.AddSingleton<IServiceBehavior, MetadataEndpointAddressServiceBehavior>();
                    services.AddSingleton<IMetadataEndpointAddressProvider, UseXForwardedHeadersForMetadataAddressProvider>();
                },
                configureHttpClient: httpClient =>
                {
                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-Proto", "https");
                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-Host", "override.local.io");
                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Forwarded-Port", "8081");
                },
                "https://override.local.io:8081");
        }

        public class UseXForwardedHeadersForMetadataAddressProvider : IMetadataEndpointAddressProvider
        {
            public Uri GetEndpointAddress(HttpRequest httpRequest)
            {
                httpRequest.Headers.TryGetValue("X-Forwarded-Proto", out var scheme);
                httpRequest.Headers.TryGetValue("X-Forwarded-Host", out var host);
                httpRequest.Headers.TryGetValue("X-Forwarded-Port", out var port);
                return new Uri($"{scheme}://{host}:{port}", UriKind.Absolute);
            }
        }
    }
}
