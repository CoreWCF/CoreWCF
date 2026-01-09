// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using System.Net;
using CoreWCF.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.ConfigurationManager.Tests
{
    public class IntegrationOfDependencyInjectionTests : TestBase
    {
        private readonly ITestOutputHelper _output;

        public IntegrationOfDependencyInjectionTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void AddOneEndpointInContainer()
        {
            AddOneEndpointInContainerCore(IPAddress.Any, 6687, "net.tcp://0.0.0.0:6687/");
        }

        [Fact]
        public void AddOneEndpointInContainerIPv6()
        {
            AddOneEndpointInContainerCore(IPAddress.IPv6Any, 6688, "net.tcp://[::]:6688/");
        }

        private void AddOneEndpointInContainerCore(IPAddress ipAddress, int port, string expectedBaseUri)
        {
            IHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output, ipAddress, port).Build();
            host.Start();
            var resolver = new DependencyResolverHelper(host);
            IServiceBuilder serviceBuilder = resolver.GetService<IServiceBuilder>();

            Assert.Single(serviceBuilder.Services);
            Assert.Single(serviceBuilder.BaseAddresses);
            Assert.Equal(typeof(SomeService).FullName, serviceBuilder.Services.Single().FullName);
            Assert.Equal(expectedBaseUri, serviceBuilder.BaseAddresses.Single().AbsoluteUri);
        }
    }
}
