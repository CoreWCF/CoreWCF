// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using CoreWCF.Configuration;
using Xunit;

namespace CoreWCF.ConfigurationManager.Tests
{
    public class SectionGroupTests
    {
        [Fact]
        public void BindingSectionTest()
        {
            string expectedName = "basicHttpBindingConfig";
            long expectedMaxReceivedMessageSize = 1073741824;
            long expectedMaxBufferSize = 1073741824;
            int expectedMaxDepth = 2147483647;
            TimeSpan expectedReceiveTimeout = TimeSpan.FromMinutes(10);

var xml = $@"
<configuration>
    <configSections>
        <sectionGroup name=""system.serviceModel"" type=""CoreWCF.Configuration.ServiceModelSectionGroup, CoreWCF.ConfigurationManager, Version = 0.2.0.0, Culture=neutral, PublicKeyToken=64a15a7b0fecbbfb"">
            <section name=""bindings"" type=""CoreWCF.Configuration.BindingsSection, CoreWCF.ConfigurationManager, Version=0.2.0.0, Culture=neutral, PublicKeyToken=64a15a7b0fecbbfb"" />
        </sectionGroup>         
    </configSections>  
    <system.serviceModel>         
        <bindings>         
            <basicHttpBinding>
                <binding name=""{expectedName}""
                         maxReceivedMessageSize=""{expectedMaxReceivedMessageSize}""
                         maxBufferSize=""{expectedMaxBufferSize}""
                         receiveTimeout=""{expectedReceiveTimeout}"">
                    <readerQuotas maxDepth=""{ expectedMaxDepth}"" />
                </binding >
            </basicHttpBinding>
            <netTcpBinding>
                <binding name=""{expectedName}""
                         maxReceivedMessageSize=""{expectedMaxReceivedMessageSize}""
                         maxBufferSize=""{expectedMaxBufferSize}""
                         receiveTimeout=""{expectedReceiveTimeout}"">
                    <readerQuotas maxDepth=""{ expectedMaxDepth}"" />
                </binding >
            </netTcpBinding>   
        </bindings>                             
    </system.serviceModel>
</configuration>";

            using (var fs = TemporaryFileStream.Create(xml))
            {
                var cfg = new ConfigurationFileMap(fs.Name);
                var configuration = System.Configuration.ConfigurationManager.OpenMappedMachineConfiguration(cfg);
                var section = ServiceModelSectionGroup.GetSectionGroup(configuration);

                var actualBasicHttpBinding = section.Bindings.BasicHttpBinding.Bindings.Cast<BasicHttpBindingElement>().First();
                var actualNetTcpBinding = section.Bindings.NetTcpBinding.Bindings.Cast<NetTcpBindingElement>().First();

                Assert.Equal(actualBasicHttpBinding.Name, expectedName);
                Assert.Equal(actualBasicHttpBinding.MaxReceivedMessageSize, expectedMaxReceivedMessageSize);
                Assert.Equal(actualBasicHttpBinding.MaxBufferSize, expectedMaxBufferSize);
                Assert.Equal(actualBasicHttpBinding.ReceiveTimeout, expectedReceiveTimeout);
                Assert.Equal(actualBasicHttpBinding.ReaderQuotas.MaxDepth, expectedMaxDepth);

                Assert.Equal(actualNetTcpBinding.Name, expectedName);
                Assert.Equal(actualNetTcpBinding.MaxReceivedMessageSize, expectedMaxReceivedMessageSize);
                Assert.Equal(actualNetTcpBinding.MaxBufferSize, expectedMaxBufferSize);
                Assert.Equal(actualNetTcpBinding.ReceiveTimeout, expectedReceiveTimeout);
                Assert.Equal(actualNetTcpBinding.ReaderQuotas.MaxDepth, expectedMaxDepth);
            }
        }

        [Fact]
        public void ServiceSectionTest()
        {
            string expectedName1 = "AnotherService";
            string expectedName2 = "SomeService";
            var xml = $@"
<configuration>
    <configSections>
        <sectionGroup name=""system.serviceModel"" type=""CoreWCF.Configuration.ServiceModelSectionGroup, CoreWCF.ConfigurationManager, Version = 0.2.0.0, Culture=neutral, PublicKeyToken=64a15a7b0fecbbfb"">
            <section name=""services"" type=""CoreWCF.Configuration.ServicesSection, CoreWCF.ConfigurationManager, Version=0.2.0.0, Culture=neutral, PublicKeyToken=64a15a7b0fecbbfb"" />
        </sectionGroup>         
    </configSections>  
    <system.serviceModel>         
        <services>
            <service name=""{expectedName1}"" />
            <service name=""{expectedName2}""
                     behaviorConfiguration=""SomeServiceBehavior"">
                <endpoint binding=""netTcpBinding""
                          bindingConfiguration=""netTcpBindingConfigService""
                          contract=""Server.Interface.SomeService""
                          address=""net.tcp://localhost:9392/"" />  
            </service>   
        </services> 
    </system.serviceModel>
</configuration>";

            using (var fs = TemporaryFileStream.Create(xml))
            {
                var cfg = new ConfigurationFileMap(fs.Name);
                var configuration = System.Configuration.ConfigurationManager.OpenMappedMachineConfiguration(cfg);
                var section = ServiceModelSectionGroup.GetSectionGroup(configuration);

                var firstService = section.Services.Services.Cast<ServiceElement>().ElementAt(0);
                var secondService = section.Services.Services.Cast<ServiceElement>().ElementAt(1);
                Assert.Equal(expectedName1, firstService.Name);
                Assert.Equal(expectedName2, secondService.Name);
            }
        }
    }
}
