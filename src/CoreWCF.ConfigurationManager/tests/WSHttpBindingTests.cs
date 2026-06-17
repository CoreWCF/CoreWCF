// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CoreWCF.ConfigurationManager.Tests
{
    public class WSHttpBindingTests : TestBase
    {
        [Fact]
        public void WSHttpBinding_WithCustomSetting()
        {
            string expectedName = "wsHttpBindingConfig";
            long expectedMaxReceivedMessageSize = 1073741824;
            long expectedMaxBufferPoolSize = 1073741824;
            int expectedMaxDepth = 2147483647;
            TimeSpan expectedReceiveTimeout = TimeSpan.FromMinutes(10);
            TimeSpan expectedDefaultTimeout = TimeSpan.FromMinutes(1);
            SecurityMode expectedSecurityMode = SecurityMode.TransportWithMessageCredential;
            MessageCredentialType clientCredType = MessageCredentialType.UserName;

            string xml = $@"
<configuration>
    <system.serviceModel>
        <bindings>
            <wsHttpBinding>
                <binding name=""{expectedName}""
                         maxReceivedMessageSize=""{expectedMaxReceivedMessageSize}""
                         maxBufferPoolSize=""{expectedMaxBufferPoolSize}""
                         receiveTimeout=""00:10:00"">
                    <security mode=""{expectedSecurityMode}"">
                    <message clientCredentialType=""{clientCredType}"" />
                     </security>
                    <readerQuotas maxDepth=""{expectedMaxDepth}"" />
                </binding >
            </wsHttpBinding>
        </bindings>
    </system.serviceModel>
</configuration>";

            using (var fs = TemporaryFileStream.Create(xml))
            {
                using (ServiceProvider provider = CreateProvider(fs.Name))
                {
                    IConfigurationHolder settingHolder = GetConfigurationHolder(provider);

                    var actualBinding = settingHolder.ResolveBinding(nameof(WSHttpBinding), expectedName) as WSHttpBinding;
                    Assert.Equal(expectedName, actualBinding.Name);
                    Assert.Equal(expectedMaxReceivedMessageSize, actualBinding.MaxReceivedMessageSize);
                    Assert.Equal(expectedDefaultTimeout, actualBinding.CloseTimeout);
                    Assert.Equal(expectedDefaultTimeout, actualBinding.OpenTimeout);
                    Assert.Equal(expectedDefaultTimeout, actualBinding.SendTimeout);
                    Assert.Equal(expectedReceiveTimeout, actualBinding.ReceiveTimeout);
                    Assert.Equal(expectedMaxBufferPoolSize, actualBinding.MaxBufferPoolSize);
                    Assert.Equal(expectedMaxDepth, actualBinding.ReaderQuotas.MaxDepth);
                    Assert.Equal(expectedSecurityMode, actualBinding.Security.Mode);
                    Assert.Equal(clientCredType, actualBinding.Security.Message.ClientCredentialType);
                }
            }
        }

        [NetCoreOnlyFact]
        public void WSHttpBinding_WithDefaultSetting()
        {
            string expectedName = "wsHttpBindingConfig";
            long expectedMaxReceivedMessageSize = 65536;
            long expectedMaxBufferPoolSize = 65536;
            TimeSpan expectedReceiveTimeout = TimeSpan.FromMinutes(10);
            TimeSpan expectedDefaultTimeout = TimeSpan.FromMinutes(1);

            string xml = $@"
<configuration>
    <system.serviceModel>
        <bindings>
            <wsHttpBinding>
                <binding name=""{expectedName}""/>
            </wsHttpBinding>
        </bindings>
    </system.serviceModel>
</configuration>";

            using (var fs = TemporaryFileStream.Create(xml))
            {
                using (ServiceProvider provider = CreateProvider(fs.Name))
                {
                    IConfigurationHolder settingHolder = GetConfigurationHolder(provider);

                    var actualBinding = settingHolder.ResolveBinding(nameof(WSHttpBinding), expectedName) as WSHttpBinding;
                    Assert.Equal(expectedName, actualBinding.Name);
                    Assert.Equal(expectedMaxReceivedMessageSize, actualBinding.MaxReceivedMessageSize);
                    Assert.Equal(expectedMaxBufferPoolSize, actualBinding.MaxBufferPoolSize);
                    Assert.Equal(expectedDefaultTimeout, actualBinding.CloseTimeout);
                    Assert.Equal(expectedDefaultTimeout, actualBinding.OpenTimeout);
                    Assert.Equal(expectedDefaultTimeout, actualBinding.SendTimeout);
                    Assert.Equal(expectedReceiveTimeout, actualBinding.ReceiveTimeout);
                    Assert.Equal(SecurityMode.Message, actualBinding.Security.Mode);
                }
            }
        }

        [NetCoreOnlyFact]
        public void WSHttpBinding_WithReliableSession()
        {
            string expectedName = "wsHttpBindingWithRS";

            string xml = $@"
<configuration>
    <system.serviceModel>
        <bindings>
            <wsHttpBinding>
                <binding name=""{expectedName}"">
                    <reliableSession enabled=""true"" ordered=""false"" inactivityTimeout=""00:05:00"" />
                    <security mode=""None""/>
                </binding>
            </wsHttpBinding>
        </bindings>
    </system.serviceModel>
</configuration>";

            using (var fs = TemporaryFileStream.Create(xml))
            {
                using (ServiceProvider provider = CreateProvider(fs.Name))
                {
                    IConfigurationHolder settingHolder = GetConfigurationHolder(provider);

                    var actualBinding = settingHolder.ResolveBinding(nameof(WSHttpBinding), expectedName) as WSHttpBinding;
                    Assert.Equal(expectedName, actualBinding.Name);
                    Assert.True(actualBinding.ReliableSession.Enabled);
                    Assert.False(actualBinding.ReliableSession.Ordered);
                    Assert.Equal(TimeSpan.FromMinutes(5), actualBinding.ReliableSession.InactivityTimeout);
                    Assert.Equal(SecurityMode.None, actualBinding.Security.Mode);
                }
            }
        }

        [NetCoreOnlyFact]
        public void WSHttpBinding_WithReliableSessionDefaults()
        {
            string expectedName = "wsHttpBindingWithRSDefaults";

            string xml = $@"
<configuration>
    <system.serviceModel>
        <bindings>
            <wsHttpBinding>
                <binding name=""{expectedName}"">
                    <reliableSession enabled=""true"" />
                    <security mode=""None""/>
                </binding>
            </wsHttpBinding>
        </bindings>
    </system.serviceModel>
</configuration>";

            using (var fs = TemporaryFileStream.Create(xml))
            {
                using (ServiceProvider provider = CreateProvider(fs.Name))
                {
                    IConfigurationHolder settingHolder = GetConfigurationHolder(provider);

                    var actualBinding = settingHolder.ResolveBinding(nameof(WSHttpBinding), expectedName) as WSHttpBinding;
                    Assert.Equal(expectedName, actualBinding.Name);
                    Assert.True(actualBinding.ReliableSession.Enabled);
                    Assert.True(actualBinding.ReliableSession.Ordered);
                    Assert.Equal(TimeSpan.FromMinutes(10), actualBinding.ReliableSession.InactivityTimeout);
                }
            }
        }
    }
}
