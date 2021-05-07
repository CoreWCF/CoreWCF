// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Linq;
using CoreWCF.Configuration;
using Microsoft.AspNetCore.Hosting;
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
            IWebHost host = ServiceHelper.CreateWebHostBuilder<Startup>(_output).Build();
            using (host)
            {
                host.Start();
                var resolver = new DependencyResolverHelper(host);
                IServiceBuilder serviceBuilder = resolver.GetService<IServiceBuilder>();

                Assert.Single(serviceBuilder.Services);
                Assert.Single(serviceBuilder.BaseAddresses);
                Assert.Equal(typeof(SomeService).FullName, serviceBuilder.Services.Single().FullName);
                Assert.Equal("net.tcp://localhost:6687/", serviceBuilder.BaseAddresses.Single().AbsoluteUri);
            }
        }
    }
}
