﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CoreWCF.ConfigurationManager.Tests
{
    public class NetHttpBindingTests : TestBase
    {
        [Fact]
        public void NetHttpBinding_WithCustomSetting()
        {
            string expectedName = "netHttpBindingConfig";
            long expectedMaxReceivedMessageSize = 1073741824;
            long expectedMaxBufferSize = 1073741824;
            int expectedMaxDepth = 2147483647;
            TimeSpan expectedReceiveTimeout = TimeSpan.FromMinutes(10);
            TimeSpan expectedDefaultTimeout = TimeSpan.FromMinutes(1);
            BasicHttpSecurityMode expectedSecurityMode = BasicHttpSecurityMode.TransportWithMessageCredential;

            string xml = $@"
<configuration> 
    <system.serviceModel>         
        <bindings>         
            <netHttpBinding>
                <binding name=""{expectedName}""
                         maxReceivedMessageSize=""{expectedMaxReceivedMessageSize}""
                         maxBufferSize=""{expectedMaxBufferSize}""
                         receiveTimeout=""00:10:00"">
                    <security mode=""{expectedSecurityMode}""/>
                    <readerQuotas maxDepth=""{expectedMaxDepth}"" />   
                </binding >
            </netHttpBinding>                             
        </bindings>                             
    </system.serviceModel>
</configuration>";

            using (var fs = TemporaryFileStream.Create(xml))
            {
                using (ServiceProvider provider = CreateProvider(fs.Name))
                {
                    IConfigurationHolder settingHolder = GetConfigurationHolder(provider);

                    var actualBinding = settingHolder.ResolveBinding(nameof(NetHttpBinding), expectedName) as NetHttpBinding;
                    Assert.Equal(expectedName, actualBinding.Name);
                    Assert.Equal(expectedMaxReceivedMessageSize, actualBinding.MaxReceivedMessageSize);
                    Assert.Equal(expectedMaxBufferSize, actualBinding.MaxBufferSize);
                    Assert.Equal(expectedDefaultTimeout, actualBinding.CloseTimeout);
                    Assert.Equal(expectedDefaultTimeout, actualBinding.OpenTimeout);
                    Assert.Equal(expectedDefaultTimeout, actualBinding.SendTimeout);
                    Assert.Equal(expectedReceiveTimeout, actualBinding.ReceiveTimeout);
                    Assert.Equal(TransferMode.Buffered, actualBinding.TransferMode);
                    Assert.Equal(expectedSecurityMode, actualBinding.Security.Mode);
                    Assert.Equal(expectedMaxDepth, actualBinding.ReaderQuotas.MaxDepth);
                }
            }

        }

        [Fact]
        public void NetHttpBinding_WithDefaultSetting()
        {
            string expectedName = "netHttpBindingConfig";
            long expectedMaxReceivedMessageSize = 65536;
            long expectedMaxBufferSize = 65536;
            TimeSpan expectedReceiveTimeout = TimeSpan.FromMinutes(10);
            TimeSpan expectedDefaultTimeout = TimeSpan.FromMinutes(1);

            string xml = $@"
<configuration> 
    <system.serviceModel>         
        <bindings>         
            <netHttpBinding>
                <binding name=""{expectedName}""/>
            </netHttpBinding>                             
        </bindings>                             
    </system.serviceModel>
</configuration>";

            using (var fs = TemporaryFileStream.Create(xml))
            {
                using (ServiceProvider provider = CreateProvider(fs.Name))
                {
                    IConfigurationHolder settingHolder = GetConfigurationHolder(provider);

                    var actualBinding = settingHolder.ResolveBinding(nameof(NetHttpBinding), expectedName) as NetHttpBinding;
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
