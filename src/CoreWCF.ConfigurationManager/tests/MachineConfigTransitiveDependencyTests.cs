using System;
using System.IO;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CoreWCF.ConfigurationManager.Tests
{
    public class MachineConfigTransitiveDependencyTests : TestBase
    {
        [Fact]
        public void MachineConfig_ShouldExist_InOutputDirectory()
        {
            // Test that CoreWCF.machine.config is copied to the output directory
            // This test validates the fix for issue #1619
            
            var configPath = Path.Combine(AppContext.BaseDirectory, "CoreWCF.machine.config");
            
            Assert.True(File.Exists(configPath), 
                $"CoreWCF.machine.config should exist at {configPath}. " +
                "This file is required for CoreWCF configuration and should be " +
                "copied to the output directory even when CoreWCF.ConfigurationManager " +
                "is referenced as a transitive dependency.");
        }
        
        [Fact]
        public void MachineConfig_ShouldContain_ServiceModelConfiguration()
        {
            // Test that the machine config contains the expected content
            var configPath = Path.Combine(AppContext.BaseDirectory, "CoreWCF.machine.config");
            
            if (File.Exists(configPath))
            {
                var content = File.ReadAllText(configPath);
                
                Assert.Contains("system.serviceModel", content);
                Assert.Contains("CoreWCF.Configuration.ServiceModelSectionGroup", content);
                Assert.Contains("CoreWCF.ConfigurationManager", content);
            }
            else
            {
                // If the file doesn't exist, fail with a helpful message
                Assert.Fail(
                    $"CoreWCF.machine.config not found at {configPath}. " +
                    "Cannot validate content. This indicates the transitive dependency issue is not fixed.");
            }
        }

        [Fact]
        public void ConfigurationManager_FullIntegration_WithTransitiveDependency()
        {
            // This is a comprehensive integration test that validates:
            // 1. CoreWCF.machine.config is present (via transitive dependency)
            // 2. ConfigurationManager can load and parse service configuration
            // 3. Services and endpoints are correctly configured
            // 4. Bindings are correctly resolved
            // This test validates the complete fix for issue #1621
            
            string expectedServiceName = typeof(SomeService).FullName;
            string expectedContractName = typeof(ISomeService).FullName;
            string expectedAddress = "net.tcp://localhost:8750/integration-test";
            string expectedEndpointName = "IntegrationTestEndpoint";
            string expectedBindingName = "integrationTestBinding";
            long expectedMaxReceivedMessageSize = int.MaxValue;
            
            string xml = $@"
<configuration> 
    <system.serviceModel>
        <bindings>         
            <netTcpBinding>
                <binding name=""{expectedBindingName}""
                         maxReceivedMessageSize=""{expectedMaxReceivedMessageSize}""
                         receiveTimeout=""00:15:00"">
                    <security mode=""None"" />
                </binding>                
            </netTcpBinding>
        </bindings> 
        <services>            
            <service name=""{expectedServiceName}"">
                <endpoint address=""{expectedAddress}""
                          name=""{expectedEndpointName}""
                          binding=""netTcpBinding""
                          bindingConfiguration=""{expectedBindingName}""
                          contract=""{expectedContractName}"" />
            </service>
        </services>                             
    </system.serviceModel>
</configuration>";

            using (var fs = TemporaryFileStream.Create(xml))
            {
                using (ServiceProvider provider = CreateProvider(fs.Name))
                {
                    // Verify that the configuration can be loaded
                    IConfigurationHolder configHolder = GetConfigurationHolder(provider);
                    Assert.NotNull(configHolder);
                    
                    // Verify that endpoints are configured
                    Assert.NotNull(configHolder.Endpoints);
                    Assert.NotEmpty(configHolder.Endpoints);
                    
                    // Verify the specific endpoint configuration
                    IXmlConfigEndpoint endpoint = GetXmlConfigEndpointByEndpointName(configHolder, expectedEndpointName);
                    Assert.NotNull(endpoint);
                    Assert.Equal(expectedServiceName, endpoint.Service.FullName);
                    Assert.Equal(expectedContractName, endpoint.Contract.FullName);
                    Assert.Equal(expectedAddress, endpoint.Address.AbsoluteUri);
                    
                    // Verify the binding configuration
                    var binding = configHolder.ResolveBinding("netTcpBinding", expectedBindingName);
                    Assert.NotNull(binding);
                    Assert.IsType<NetTcpBinding>(binding);
                    
                    var netTcpBinding = (NetTcpBinding)binding;
                    Assert.Equal(expectedBindingName, netTcpBinding.Name);
                    Assert.Equal(expectedMaxReceivedMessageSize, netTcpBinding.MaxReceivedMessageSize);
                    Assert.Equal(TimeSpan.FromMinutes(15), netTcpBinding.ReceiveTimeout);
                    Assert.Equal(SecurityMode.None, netTcpBinding.Security.Mode);
                }
            }
        }

        [Fact]
        public void ConfigurationManager_CanLoad_MultipleBindingsAndServices()
        {
            // Integration test to validate that ConfigurationManager correctly handles
            // complex configurations with multiple services, bindings, and endpoints
            // This ensures the transitive dependency scenario works for real-world use cases
            
            string expectedServiceName = typeof(SomeService).FullName;
            string expectedContractName = typeof(ISomeService).FullName;
            
            string xml = $@"
<configuration> 
    <system.serviceModel>
        <bindings>         
            <netTcpBinding>
                <binding name=""tcpBinding1"" maxReceivedMessageSize=""1048576"" />
                <binding name=""tcpBinding2"" maxReceivedMessageSize=""2097152"" />
            </netTcpBinding>
            <basicHttpBinding>
                <binding name=""httpBinding1"">
                    <security mode=""None"" />
                </binding>
            </basicHttpBinding>
        </bindings> 
        <services>            
            <service name=""{expectedServiceName}"">
                <endpoint address=""net.tcp://localhost:8751/service1""
                          name=""TcpEndpoint1""
                          binding=""netTcpBinding""
                          bindingConfiguration=""tcpBinding1""
                          contract=""{expectedContractName}"" />
                <endpoint address=""net.tcp://localhost:8752/service2""
                          name=""TcpEndpoint2""
                          binding=""netTcpBinding""
                          bindingConfiguration=""tcpBinding2""
                          contract=""{expectedContractName}"" />
                <endpoint address=""http://localhost:8080/service""
                          name=""HttpEndpoint""
                          binding=""basicHttpBinding""
                          bindingConfiguration=""httpBinding1""
                          contract=""{expectedContractName}"" />
            </service>
        </services>                             
    </system.serviceModel>
</configuration>";

            using (var fs = TemporaryFileStream.Create(xml))
            {
                using (ServiceProvider provider = CreateProvider(fs.Name))
                {
                    IConfigurationHolder configHolder = GetConfigurationHolder(provider);
                    
                    // Verify we have all three endpoints
                    Assert.Equal(3, configHolder.Endpoints.Count);
                    
                    // Verify each endpoint is correctly configured
                    var tcpEndpoint1 = GetXmlConfigEndpointByEndpointName(configHolder, "TcpEndpoint1");
                    Assert.NotNull(tcpEndpoint1);
                    Assert.Equal("net.tcp://localhost:8751/service1", tcpEndpoint1.Address.AbsoluteUri);
                    
                    var tcpEndpoint2 = GetXmlConfigEndpointByEndpointName(configHolder, "TcpEndpoint2");
                    Assert.NotNull(tcpEndpoint2);
                    Assert.Equal("net.tcp://localhost:8752/service2", tcpEndpoint2.Address.AbsoluteUri);
                    
                    var httpEndpoint = GetXmlConfigEndpointByEndpointName(configHolder, "HttpEndpoint");
                    Assert.NotNull(httpEndpoint);
                    Assert.Equal("http://localhost:8080/service", httpEndpoint.Address.AbsoluteUri);
                    
                    // Verify bindings are correctly resolved
                    var binding1 = configHolder.ResolveBinding("netTcpBinding", "tcpBinding1") as NetTcpBinding;
                    Assert.NotNull(binding1);
                    Assert.Equal(1048576, binding1.MaxReceivedMessageSize);
                    
                    var binding2 = configHolder.ResolveBinding("netTcpBinding", "tcpBinding2") as NetTcpBinding;
                    Assert.NotNull(binding2);
                    Assert.Equal(2097152, binding2.MaxReceivedMessageSize);
                    
                    var httpBinding = configHolder.ResolveBinding("basicHttpBinding", "httpBinding1") as BasicHttpBinding;
                    Assert.NotNull(httpBinding);
                    Assert.Equal(BasicHttpSecurityMode.None, httpBinding.Security.Mode);
                }
            }
        }
    }
}