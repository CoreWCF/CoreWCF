// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Configuration;
using System.Linq;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using Xunit;

namespace CoreWCF.ConfigurationManager.Tests
{
    public class SectionGroupTests
    {
        [Fact]
        public void BindingSection_WithNetHttpBindingTest()
        {
            string expectedName = "netHttpBindingConfig";
            long expectedMaxReceivedMessageSize = 1073741824;
            long expectedMaxBufferSize = 1073741824;
            int expectedMaxDepth = 2147483647;
            TimeSpan expectedReceiveTimeout = TimeSpan.FromMinutes(10);
            BasicHttpSecurityMode expectedSecurityMode = BasicHttpSecurityMode.TransportWithMessageCredential;

            string xml = $@"
<configuration>
    <configSections>
        <sectionGroup name=""system.serviceModel"" type=""CoreWCF.Configuration.ServiceModelSectionGroup, CoreWCF.ConfigurationManager"">
            <section name=""bindings"" type=""CoreWCF.Configuration.BindingsSection, CoreWCF.ConfigurationManager"" />
        </sectionGroup>         
    </configSections>  
    <system.serviceModel>         
        <bindings>
            <netHttpBinding>
                <binding name=""{expectedName}""
                         maxReceivedMessageSize=""{expectedMaxReceivedMessageSize}""
                         maxBufferSize=""{expectedMaxBufferSize}""
                         receiveTimeout=""{expectedReceiveTimeout}"">
                    <security mode=""{expectedSecurityMode}""/>      
                    <readerQuotas maxDepth=""{expectedMaxDepth}"" />
                </binding>
            </netHttpBinding>           
        </bindings>                             
    </system.serviceModel>
</configuration>";

            using (var fs = TemporaryFileStream.Create(xml))
            {
                ServiceModelSectionGroup section = GetSectionFromXml(fs);

                NetHttpBindingElement actualBinding = section.Bindings.NetHttpBinding.Bindings.Cast<NetHttpBindingElement>().First();

                Assert.Equal(expectedName, actualBinding.Name);
                Assert.Equal(expectedMaxReceivedMessageSize, actualBinding.MaxReceivedMessageSize);
                Assert.Equal(expectedMaxBufferSize, actualBinding.MaxBufferSize);
                Assert.Equal(expectedReceiveTimeout, actualBinding.ReceiveTimeout);
                Assert.Equal(expectedSecurityMode, actualBinding.Security.Mode);
                Assert.Equal(expectedMaxDepth, actualBinding.ReaderQuotas.MaxDepth);
            }
        }

        [Fact]
        public void BindingSection_WithBasicHttpBindingTest()
        {
            string expectedName = "basicHttpBindingConfig";
            long expectedMaxReceivedMessageSize = 1073741824;
            long expectedMaxBufferSize = 1073741824;
            int expectedMaxDepth = 2147483647;
            TimeSpan expectedReceiveTimeout = TimeSpan.FromMinutes(10);
            BasicHttpSecurityMode expectedSecurityMode = BasicHttpSecurityMode.TransportWithMessageCredential;

            string xml = $@"
<configuration>
    <configSections>
        <sectionGroup name=""system.serviceModel"" type=""CoreWCF.Configuration.ServiceModelSectionGroup, CoreWCF.ConfigurationManager"">
            <section name=""bindings"" type=""CoreWCF.Configuration.BindingsSection, CoreWCF.ConfigurationManager"" />
        </sectionGroup>         
    </configSections>  
    <system.serviceModel>         
        <bindings>         
            <basicHttpBinding>
                <binding name=""{expectedName}""
                         maxReceivedMessageSize=""{expectedMaxReceivedMessageSize}""
                         maxBufferSize=""{expectedMaxBufferSize}""
                         receiveTimeout=""{expectedReceiveTimeout}"">
                    <security mode=""{expectedSecurityMode}""/>      
                    <readerQuotas maxDepth=""{expectedMaxDepth}"" />
                </binding>
            </basicHttpBinding>           
        </bindings>                             
    </system.serviceModel>
</configuration>";

            using (var fs = TemporaryFileStream.Create(xml))
            {
                ServiceModelSectionGroup section = GetSectionFromXml(fs);

                BasicHttpBindingElement actualBinding = section.Bindings.BasicHttpBinding.Bindings.Cast<BasicHttpBindingElement>().First();

                Assert.Equal(expectedName, actualBinding.Name);
                Assert.Equal(expectedMaxReceivedMessageSize, actualBinding.MaxReceivedMessageSize);
                Assert.Equal(expectedMaxBufferSize, actualBinding.MaxBufferSize);
                Assert.Equal(expectedReceiveTimeout, actualBinding.ReceiveTimeout);
                Assert.Equal(expectedSecurityMode, actualBinding.Security.Mode);
                Assert.Equal(expectedMaxDepth, actualBinding.ReaderQuotas.MaxDepth);
            }
        }

        [Fact]
        public void BindingSection_WithWSHttpBindingTest()
        {
            string expectedName = "wsHttpBindingConfig";
            long expectedMaxReceivedMessageSize = 1073741824;
            long expectedMaxBufferPoolSize = 1073741824;
            int expectedMaxDepth = 2147483647;
            TimeSpan expectedReceiveTimeout = TimeSpan.FromMinutes(10);
            SecurityMode expectedSecurityMode = SecurityMode.TransportWithMessageCredential;

            string xml = $@"
<configuration>
    <configSections>
        <sectionGroup name=""system.serviceModel"" type=""CoreWCF.Configuration.ServiceModelSectionGroup, CoreWCF.ConfigurationManager"">
            <section name=""bindings"" type=""CoreWCF.Configuration.BindingsSection, CoreWCF.ConfigurationManager"" />
        </sectionGroup>         
    </configSections>  
    <system.serviceModel>         
        <bindings>
            <wsHttpBinding>
                <binding name=""{expectedName}""
                         maxReceivedMessageSize=""{expectedMaxReceivedMessageSize}""
                         maxBufferPoolSize=""{expectedMaxBufferPoolSize}""
                         receiveTimeout=""{expectedReceiveTimeout}"">
                    <security mode=""{expectedSecurityMode}""/>      
                    <readerQuotas maxDepth=""{expectedMaxDepth}""/>
                </binding>
            </wsHttpBinding>           
        </bindings>                             
    </system.serviceModel>
</configuration>";

            using (var fs = TemporaryFileStream.Create(xml))
            {
                ServiceModelSectionGroup section = GetSectionFromXml(fs);

                WSHttpBindingElement actualBinding = section.Bindings.wsHttpBinding.Bindings.Cast<WSHttpBindingElement>().First();

                Assert.Equal(expectedName, actualBinding.Name);
                Assert.Equal(expectedMaxReceivedMessageSize, actualBinding.MaxReceivedMessageSize);
                Assert.Equal(expectedMaxBufferPoolSize, actualBinding.MaxBufferPoolSize);
                Assert.Equal(expectedReceiveTimeout, actualBinding.ReceiveTimeout);
                Assert.Equal(expectedSecurityMode, actualBinding.Security.Mode);
                Assert.Equal(expectedMaxDepth, actualBinding.ReaderQuotas.MaxDepth);
            }
        }

        [Fact]
        public void BindingSection_WithNetTcpBindingTest()
        {
            string expectedName = "netTcpBindingConfig";
            long expectedMaxReceivedMessageSize = 1073741824;
            long expectedMaxBufferSize = 1073741824;
            int expectedMaxDepth = 2147483647;
            TimeSpan expectedReceiveTimeout = TimeSpan.FromMinutes(10);

            string xml = $@"
<configuration>
    <configSections>
        <sectionGroup name=""system.serviceModel"" type=""CoreWCF.Configuration.ServiceModelSectionGroup, CoreWCF.ConfigurationManager"">
            <section name=""bindings"" type=""CoreWCF.Configuration.BindingsSection, CoreWCF.ConfigurationManager"" />
        </sectionGroup>         
    </configSections>  
    <system.serviceModel>         
        <bindings>            
            <netTcpBinding>
                <binding name=""{expectedName}""
                         maxReceivedMessageSize=""{expectedMaxReceivedMessageSize}""
                         maxBufferSize=""{expectedMaxBufferSize}""
                         receiveTimeout=""{expectedReceiveTimeout}"">
                    <readerQuotas maxDepth=""{expectedMaxDepth}"" />
                </binding>
            </netTcpBinding>   
        </bindings>                             
    </system.serviceModel>
</configuration>";

            using (var fs = TemporaryFileStream.Create(xml))
            {
                ServiceModelSectionGroup section = GetSectionFromXml(fs);

                NetTcpBindingElement actualBinding = section.Bindings.NetTcpBinding.Bindings.Cast<NetTcpBindingElement>().First();

                Assert.Equal(expectedName, actualBinding.Name);
                Assert.Equal(expectedMaxReceivedMessageSize, actualBinding.MaxReceivedMessageSize);
                Assert.Equal(expectedMaxBufferSize, actualBinding.MaxBufferSize);
                Assert.Equal(expectedReceiveTimeout, actualBinding.ReceiveTimeout);
                Assert.Equal(expectedMaxDepth, actualBinding.ReaderQuotas.MaxDepth);
            }
        }

        [Fact]
        public void BindingSection_WithWebHttpBindingTest()
        {
            string expectedName = "webHttpBindingConfig";
            long expectedMaxReceivedMessageSize = 1073741824;
            long expectedMaxBufferSize = 1073741824;
            int expectedMaxDepth = 2147483647;
            TimeSpan expectedReceiveTimeout = TimeSpan.FromMinutes(10);
            WebHttpSecurityMode expectedSecurityMode = WebHttpSecurityMode.TransportCredentialOnly;

            string xml = $@"
<configuration>
    <configSections>
        <sectionGroup name=""system.serviceModel"" type=""CoreWCF.Configuration.ServiceModelSectionGroup, CoreWCF.ConfigurationManager"">
            <section name=""bindings"" type=""CoreWCF.Configuration.BindingsSection, CoreWCF.ConfigurationManager"" />
        </sectionGroup>         
    </configSections>  
    <system.serviceModel>         
        <bindings>            
            <webHttpBinding>
                <binding name=""{expectedName}""
                         maxReceivedMessageSize=""{expectedMaxReceivedMessageSize}""
                         maxBufferSize=""{expectedMaxBufferSize}""
                         receiveTimeout=""{expectedReceiveTimeout}"">
                    <security mode=""{expectedSecurityMode}""/>      
                    <readerQuotas maxDepth=""{expectedMaxDepth}"" />
                </binding>
            </webHttpBinding>   
        </bindings>                             
    </system.serviceModel>
</configuration>";

            using (var fs = TemporaryFileStream.Create(xml))
            {
                ServiceModelSectionGroup section = GetSectionFromXml(fs);

                WebHttpBindingElement actualBinding = section.Bindings.WebHttpBinding.Bindings.Cast<WebHttpBindingElement>().First();

                Assert.Equal(expectedName, actualBinding.Name);
                Assert.Equal(expectedMaxReceivedMessageSize, actualBinding.MaxReceivedMessageSize);
                Assert.Equal(expectedMaxBufferSize, actualBinding.MaxBufferSize);
                Assert.Equal(expectedReceiveTimeout, actualBinding.ReceiveTimeout);
                Assert.Equal(expectedMaxDepth, actualBinding.ReaderQuotas.MaxDepth);
                Assert.Equal(expectedSecurityMode, actualBinding.Security.Mode);
            }
        }

        [Fact]
        public void ServiceSectionTest()
        {
            string expectedName1 = "AnotherService";
            string expectedName2 = "SomeService";
            string expectedBehavior = "SomeServiceBehavior";
            string expectedBindingConfiguration = "netTcpBindingConfigService";
            string expectedContract = "Server.Interface.SomeService";
            string expectedAddress = "net.tcp://localhost:9392/";
            string xml = $@"
<configuration>
    <configSections>
        <sectionGroup name=""system.serviceModel"" type=""CoreWCF.Configuration.ServiceModelSectionGroup, CoreWCF.ConfigurationManager"">
            <section name=""services"" type=""CoreWCF.Configuration.ServicesSection, CoreWCF.ConfigurationManager"" />
        </sectionGroup>         
    </configSections>  
    <system.serviceModel>         
        <services>
            <service name=""{expectedName1}"" />
            <service name=""{expectedName2}""
                     behaviorConfiguration=""{expectedBehavior}"">
                <endpoint binding=""netTcpBinding""
                          bindingConfiguration=""{expectedBindingConfiguration}""
                          contract=""{expectedContract}""
                          address=""{expectedAddress}"" />  
            </service>   
        </services> 
    </system.serviceModel>
</configuration>";

            using (var fs = TemporaryFileStream.Create(xml))
            {
                ServiceModelSectionGroup section = GetSectionFromXml(fs);

                ServiceElement firstService = section.Services.Services.Cast<ServiceElement>().ElementAt(0);
                ServiceElement secondService = section.Services.Services.Cast<ServiceElement>().ElementAt(1);
                ServiceEndpointElement endpoint = secondService.Endpoints.Cast<ServiceEndpointElement>().Single();
                Assert.Equal(expectedName1, firstService.Name);
                Assert.Equal(expectedName2, secondService.Name);
                Assert.Equal(expectedBehavior, secondService.BehaviorConfiguration);
                Assert.Equal(expectedBindingConfiguration, endpoint.BindingConfiguration);
                Assert.Equal(expectedContract, endpoint.Contract);
                Assert.Equal(expectedAddress, endpoint.Address.OriginalString);
            }
        }


        private static ServiceModelSectionGroup GetSectionFromXml(TemporaryFileStream fs)
        {
            var cfg = new ConfigurationFileMap(fs.Name);
            System.Configuration.Configuration configuration = System.Configuration.ConfigurationManager.OpenMappedMachineConfiguration(cfg);
            var section = ServiceModelSectionGroup.GetSectionGroup(configuration);
            return section;
        }
    }
}
