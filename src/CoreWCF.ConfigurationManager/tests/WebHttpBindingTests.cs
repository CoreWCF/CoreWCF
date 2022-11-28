// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CoreWCF.ConfigurationManager.Tests
{
    public class WebHttpBindingTests : TestBase
    {
        [Fact]
        public void WebHttpBinding_WithDefaultSetting()
        {
            string expectedName = "webHttpBindingConfig";
            long expectedMaxReceivedMessageSize = 65536;
            long expectedMaxBufferSize = 65536;
            TimeSpan expectedReceiveTimeout = TimeSpan.FromMinutes(10);
            TimeSpan expectedDefaultTimeout = TimeSpan.FromMinutes(1);

            string xml = $@"
<configuration> 
    <system.serviceModel>         
        <bindings>         
            <webHttpBinding>
                <binding name=""{expectedName}""/>
            </webHttpBinding>                             
        </bindings>                             
   </system.serviceModel>
</configuration>";

            using (var fs = TemporaryFileStream.Create(xml))
            {
                using (ServiceProvider provider = CreateProvider(fs.Name))
                {
                    IConfigurationHolder settingHolder = GetConfigurationHolder(provider);

                    var actualBinding = settingHolder.ResolveBinding(nameof(WebHttpBinding), expectedName) as WebHttpBinding;
                    Assert.Equal(expectedName, actualBinding.Name);
                    Assert.Equal(expectedMaxReceivedMessageSize, actualBinding.MaxReceivedMessageSize);
                    Assert.Equal(expectedMaxBufferSize, actualBinding.MaxBufferSize);
                    Assert.Equal(expectedDefaultTimeout, actualBinding.CloseTimeout);
                    Assert.Equal(expectedDefaultTimeout, actualBinding.OpenTimeout);
                    Assert.Equal(expectedDefaultTimeout, actualBinding.SendTimeout);
                    Assert.Equal(expectedReceiveTimeout, actualBinding.ReceiveTimeout);
                    Assert.Equal(TransferMode.Buffered, actualBinding.TransferMode);
                    Assert.Equal(WebHttpSecurityMode.None, actualBinding.Security.Mode);
                }
            }
        }

        [Fact]
        public void WebHttpBinding_WithCustomSetting()
        {
            string expectedName = "webHttpBindingConfig";
            long expectedMaxReceivedMessageSize = 1073741824;
            long expectedMaxBufferSize = 1073741824;
            int expectedMaxDepth = 2147483647;
            TimeSpan expectedReceiveTimeout = TimeSpan.FromMinutes(10);
            TimeSpan expectedDefaultTimeout = TimeSpan.FromMinutes(1);
            WebHttpSecurityMode expectedSecurityMode = WebHttpSecurityMode.TransportCredentialOnly;

            string xml = $@"
<configuration> 
    <system.serviceModel>         
        <bindings>         
            <webHttpBinding>
                <binding name=""{expectedName}""
                         maxReceivedMessageSize=""{expectedMaxReceivedMessageSize}""
                         maxBufferSize=""{expectedMaxBufferSize}""
                         receiveTimeout=""{expectedReceiveTimeout}"">
                <security mode=""{expectedSecurityMode}""/>
                <readerQuotas maxDepth=""{expectedMaxDepth}"" />   
                </binding >
            </webHttpBinding>                             
        </bindings>                             
    </system.serviceModel>
</configuration>";

            using (var fs = TemporaryFileStream.Create(xml))
            {
                using (ServiceProvider provider = CreateProvider(fs.Name))
                {
                    IConfigurationHolder settingHolder = GetConfigurationHolder(provider);

                    var actualBinding = settingHolder.ResolveBinding(nameof(WebHttpBinding), expectedName) as WebHttpBinding;
                    Assert.Equal(expectedName, actualBinding.Name);
                    Assert.Equal(expectedMaxReceivedMessageSize, actualBinding.MaxReceivedMessageSize);
                    Assert.Equal(expectedMaxBufferSize, actualBinding.MaxBufferSize);
                    Assert.Equal(expectedDefaultTimeout, actualBinding.CloseTimeout);
                    Assert.Equal(expectedDefaultTimeout, actualBinding.OpenTimeout);
                    Assert.Equal(expectedDefaultTimeout, actualBinding.SendTimeout);
                    Assert.Equal(expectedReceiveTimeout, actualBinding.ReceiveTimeout);
                    Assert.Equal(TransferMode.Buffered, actualBinding.TransferMode);
                    Assert.Equal(expectedMaxDepth, actualBinding.ReaderQuotas.MaxDepth);
                    Assert.Equal(expectedSecurityMode, actualBinding.Security.Mode);
                }
            }
        }
    }
}
