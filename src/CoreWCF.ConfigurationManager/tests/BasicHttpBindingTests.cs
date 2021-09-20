// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CoreWCF.ConfigurationManager.Tests
{
    public class BasicHttpBindingTests : TestBase
    {
        [Fact]
        public void BasicHttpBinding_WithCustomSetting()
        {
            string expectedName = "basicHttpBindingConfig";
            long expectedMaxReceivedMessageSize = 1073741824;
            long expectedMaxBufferSize = 1073741824;
            int expectedMaxDepth = 2147483647;
            TimeSpan expectedReceiveTimeout = TimeSpan.FromMinutes(10);
            TimeSpan expectedDefaultTimeout = TimeSpan.FromMinutes(1);
            BasicHttpSecurityMode expectedSecurityMode = BasicHttpSecurityMode.TransportWithMessageCredential;
            BasicHttpMessageCredentialType clientCredType = BasicHttpMessageCredentialType.Certificate;

            string xml = $@"
<configuration> 
    <system.serviceModel>         
        <bindings>         
            <basicHttpBinding>
                <binding name=""{expectedName}""
                         maxReceivedMessageSize=""{expectedMaxReceivedMessageSize}""
                         maxBufferSize=""{expectedMaxBufferSize}""
                         receiveTimeout=""00:10:00"">
                    <security mode=""{expectedSecurityMode}"">
                     <message clientCredentialType=""{clientCredType}"" />
                     </security>
                    <readerQuotas maxDepth=""{expectedMaxDepth}"" />   
                </binding >
            </basicHttpBinding>                             
        </bindings>                             
    </system.serviceModel>
</configuration>";

            using (var fs = TemporaryFileStream.Create(xml))
            {
                using (ServiceProvider provider = CreateProvider(fs.Name))
                {
                    IConfigurationHolder settingHolder = GetConfigurationHolder(provider);

                    var actualBinding = settingHolder.ResolveBinding(nameof(BasicHttpBinding), expectedName) as BasicHttpBinding;
                    Assert.Equal(expectedName, actualBinding.Name);
                    Assert.Equal(expectedMaxReceivedMessageSize, actualBinding.MaxReceivedMessageSize);
                    Assert.Equal(expectedMaxBufferSize, actualBinding.MaxBufferSize);
                    Assert.Equal(expectedDefaultTimeout, actualBinding.CloseTimeout);
                    Assert.Equal(expectedDefaultTimeout, actualBinding.OpenTimeout);
                    Assert.Equal(expectedDefaultTimeout, actualBinding.SendTimeout);
                    Assert.Equal(expectedReceiveTimeout, actualBinding.ReceiveTimeout);
                    Assert.Equal(TransferMode.Buffered, actualBinding.TransferMode);
                    Assert.Equal(expectedSecurityMode, actualBinding.Security.Mode);
                    Assert.Equal(clientCredType, actualBinding.Security.Message.ClientCredentialType);
                    Assert.Equal(expectedMaxDepth, actualBinding.ReaderQuotas.MaxDepth);
                }
            }
        }

        [Fact]
        public void BasicHttpBinding_WithDefaultSetting()
        {
            string expectedName = "basicHttpBindingConfig";
            long expectedMaxReceivedMessageSize = 65536;
            long expectedMaxBufferSize = 65536;
            TimeSpan expectedReceiveTimeout = TimeSpan.FromMinutes(10);
            TimeSpan expectedDefaultTimeout = TimeSpan.FromMinutes(1);

            string xml = $@"
<configuration> 
    <system.serviceModel>         
        <bindings>         
            <basicHttpBinding>
                <binding name=""{expectedName}""/>
            </basicHttpBinding>                             
        </bindings>                             
    </system.serviceModel>
</configuration>";

            using (var fs = TemporaryFileStream.Create(xml))
            {
                using (ServiceProvider provider = CreateProvider(fs.Name))
                {
                    IConfigurationHolder settingHolder = GetConfigurationHolder(provider);

                    var actualBinding = settingHolder.ResolveBinding(nameof(BasicHttpBinding), expectedName) as BasicHttpBinding;
                    Assert.Equal(expectedName, actualBinding.Name);
                    Assert.Equal(expectedMaxReceivedMessageSize, actualBinding.MaxReceivedMessageSize);
                    Assert.Equal(expectedMaxBufferSize, actualBinding.MaxBufferSize);
                    Assert.Equal(expectedDefaultTimeout, actualBinding.CloseTimeout);
                    Assert.Equal(expectedDefaultTimeout, actualBinding.OpenTimeout);
                    Assert.Equal(expectedDefaultTimeout, actualBinding.SendTimeout);
                    Assert.Equal(expectedReceiveTimeout, actualBinding.ReceiveTimeout);
                    Assert.Equal(TransferMode.Buffered, actualBinding.TransferMode);
                    Assert.Equal(BasicHttpSecurityMode.None, actualBinding.Security.Mode);
                }
            }
        }
    }
}
