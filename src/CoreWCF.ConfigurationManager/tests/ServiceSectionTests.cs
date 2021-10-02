// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CoreWCF.ConfigurationManager.Tests
{
    public class ServiceSectionTests : TestBase
    {
        [Fact]
        public void ServiceSectionTest()
        {
            string expectedServiceName = typeof(SomeService).FullName;
            string expectedContractName = typeof(ISomeService).FullName;
            string expectedAddress = "net.tcp://localhost:8740/";
            string expectedEndpointName = "SomeEndpoint";
            string xml = $@"
<configuration> 
    <system.serviceModel>
        <bindings>         
            <netTcpBinding>
                <binding name=""testBinding""/>                
            </netTcpBinding>
        </bindings> 
        <services>            
            <service name=""{expectedServiceName}"">
                <endpoint address=""{expectedAddress}""
                          name=""{expectedEndpointName}""
                          binding=""netTcpBinding""
                          bindingConfiguration=""testBinding""
                          contract=""{expectedContractName}"" />
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
                    Assert.Equal(expectedContractName, endpoint.Contract.FullName);
                    Assert.Equal(expectedAddress, endpoint.Address.AbsoluteUri);
                }
            }
        }

        [Fact]
        public void MultipleServiceTestWithSecurityMode()
        {
            string expectedServiceName = typeof(SomeService).FullName;
            string expectedContractName = typeof(ISomeService).FullName;
            string expectedNetTCPAddress = "net.tcp://localhost:8740/";
            string expectedBasicHttpAddress = "http://localhost:8000/";
            string expectedEndpointNameForBasicHttp = "SomeEndpoint";
            string xml = $@"
<configuration> 
    <system.serviceModel>
        <bindings>         
            <netTcpBinding>
                <binding name=""testBinding"">
                 <security mode=""None"" />
                 </binding>
            </netTcpBinding>
         <basicHttpBinding>
        <binding name=""Basic"">
          <security mode=""TransportWithMessageCredential"">
             <message clientCredentialType=""Certificate""/>
            </security >
          </binding >
        </basicHttpBinding >
          </bindings> 
        <services>            
            <service name=""{expectedServiceName}"">
                <endpoint address=""{expectedNetTCPAddress}""
                          binding=""netTcpBinding""
                          bindingConfiguration=""testBinding""
                          contract=""{expectedContractName}"" />
                <endpoint address=""{expectedBasicHttpAddress}""
                          name=""{expectedEndpointNameForBasicHttp}""
                          binding=""netTcpBinding""
                          bindingConfiguration=""testBinding""
                          contract=""{expectedContractName}"" />
            </service>
        </services>                             
    </system.serviceModel>
</configuration>";

            using (var fs = TemporaryFileStream.Create(xml))
            {
                using (ServiceProvider provider = CreateProvider(fs.Name))
                {
                    IConfigurationHolder settingHolder = GetConfigurationHolder(provider);
                    IXmlConfigEndpoint endpoint = GetXmlConfigEndpointByEndpointName(settingHolder, expectedEndpointNameForBasicHttp);
                    Assert.Equal(expectedServiceName, endpoint.Service.FullName);
                    Assert.Equal(expectedContractName, endpoint.Contract.FullName);
                    Assert.Equal(expectedBasicHttpAddress, endpoint.Address.AbsoluteUri);

                    endpoint = GetXmlConfigEndpointByEndpointName(settingHolder, string.Empty);
                    Assert.Equal(expectedServiceName, endpoint.Service.FullName);
                    Assert.Equal(expectedContractName, endpoint.Contract.FullName);
                    Assert.Equal(expectedNetTCPAddress, endpoint.Address.AbsoluteUri);
                }
            }
        }
    }
}
