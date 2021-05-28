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
                    IConfigurationHolder settingHolder = provider.GetService<IConfigurationHolder>();
                    settingHolder.Initialize();

                    IXmlConfigEndpoint endpoint = settingHolder.GetXmlConfigEndpoint(expectedEndpointName);
                    Assert.Equal(expectedServiceName, endpoint.Service.FullName);
                    Assert.Equal(expectedContractName, endpoint.Contract.FullName);
                    Assert.Equal(expectedAddress, endpoint.Address.AbsoluteUri);
                }
            }
        }
    }
}
