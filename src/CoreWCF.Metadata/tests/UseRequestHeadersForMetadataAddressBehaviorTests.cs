// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Threading.Tasks;
using CoreWCF.Description;
using CoreWCF.Metadata.Tests.Helpers;
using Microsoft.Extensions.DependencyInjection;
using ServiceContract;
using Services;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Metadata.Tests
{
    public class UseRequestHeadersForMetadataAddressBehaviorTests
    {
        private readonly ITestOutputHelper _output;

        public UseRequestHeadersForMetadataAddressBehaviorTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Theory]
        [InlineData("override.local.io:8081", "http://override.local.io:8081")]
        [InlineData("override.local.io", "http://override.local.io")]
        public async Task WithHostHeader(string hostHeader, string expectedEndpointAddress)
        {
            await TestHelper.RunSingleWsdlTestAsync<SimpleEchoService, IEchoService>(new BasicHttpBinding(),
                _output,
                configureServices: services =>
                {
                    services.AddSingleton<IServiceBehavior, UseRequestHeadersForMetadataAddressBehavior>();
                },
                configureHttpClient: httpClient =>
                {
                    httpClient.DefaultRequestHeaders.Host = hostHeader;
                },
                expectedEndpointAddress);
        }

        [Fact]
        public async Task WithoutHostHeader()
        {
            await TestHelper.RunSingleWsdlTestAsync<SimpleEchoService, IEchoService>(new BasicHttpBinding(),
                _output,
                configureServices: services =>
                {
                    services.AddSingleton<IServiceBehavior, UseRequestHeadersForMetadataAddressBehavior>();
                });
        }
    }
}
