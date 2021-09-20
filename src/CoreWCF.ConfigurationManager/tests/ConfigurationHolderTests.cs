// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CoreWCF.ConfigurationManager.Tests
{
    public class ConfigurationHolderTests : TestBase
    {
        [Fact]
        public void CreateDefaultBindingTest()
        {
            string expectedAddress = "net.tcp://localhost:8740/";
            string expectedEndpointName = "SomeEndpoint";
            string expectedServiceName = typeof(SomeService).FullName;
            string xml = $@"
<configuration> 
    <system.serviceModel>
        <services>
            <service name=""{expectedServiceName}"">
                  <endpoint address=""{expectedAddress}""
                          name=""{expectedEndpointName}""
                          binding=""netTcpBinding""                       
                          contract=""{typeof(ISomeService).FullName}"" />
            </service>
        </services>
   </system.serviceModel>
</configuration>";

            using (var fs = TemporaryFileStream.Create(xml))
            {
                using (ServiceProvider provider = CreateProvider(fs.Name))
                {
                    IConfigurationHolder settingHolder = GetConfigurationHolder(provider);
                    IXmlConfigEndpoint endpoint = GetXmlConfigEndpointByEndpointName(settingHolder, expectedEndpointName);
                    Assert.Equal(expectedServiceName, endpoint.Service.FullName);
                    Assert.IsType<NetTcpBinding>(endpoint.Binding);
                    Assert.Equal("NetTcpBinding", endpoint.Binding.Name);
                }
            }
        }

        [Fact]
        public void GetXmlConfigEndpoint_WithEmptyEndpointTest()
        {
            string xml = $@"
<configuration> 
    <system.serviceModel>
        <services>
            <service name=""{typeof(SomeService).FullName}"">
                  <endpoint address=""expectedAddress""
                          name=""expectedEndpointName""
                          binding=""netTcpBinding""                       
                          contract=""{typeof(ISomeService).FullName}"" />
            </service>
        </services>
   </system.serviceModel>
</configuration>";

            using (var fs = TemporaryFileStream.Create(xml))
            {
                using (ServiceProvider provider = CreateProvider(fs.Name))
                {
                    IConfigurationHolder settingHolder = GetConfigurationHolder(provider);

                    Assert.Throws<EndpointNotFoundException>(() => GetXmlConfigEndpointByEndpointName(settingHolder, string.Empty));
                }
            }
        }

        [Fact]
        public void ResolveBinding_WithUnknownBindingTest()
        {
            string xml = $@"
<configuration> 
    <system.serviceModel>
        <services>
            <service name=""{typeof(SomeService).FullName}"">
                  <endpoint address=""expectedAddress""
                          name=""expectedEndpointName""
                          binding=""netTcpBinding""                       
                          contract=""{typeof(ISomeService).FullName}"" />
            </service>
        </services>
   </system.serviceModel>
</configuration>";

            using (var fs = TemporaryFileStream.Create(xml))
            {
                using (ServiceProvider provider = CreateProvider(fs.Name))
                {
                    IConfigurationHolder settingHolder = GetConfigurationHolder(provider);

                    Assert.Throws<BindingNotFoundException>(() => settingHolder.ResolveBinding(nameof(NetTcpBinding), "unknown"));
                }
            }
        }
    }
}
