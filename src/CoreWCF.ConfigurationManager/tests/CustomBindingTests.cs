// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Authentication.ExtendedProtection;
using System.Text;
using CoreWCF.Channels;
using CoreWCF.Configuration;
using CoreWCF.Security;
using CoreWCF.Security.Tokens;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CoreWCF.ConfigurationManager.Tests
{
    public class CustomBindingTests : TestBase
    {
        #region CustomBinding

        [Fact]
        public void CustomBinding_WithSettings()
        {
            string expectedName = "customBinding";
            TimeSpan expectedReceiveTimeout = TimeSpan.FromMinutes(5);
            TimeSpan expectedDefaultTimeout = TimeSpan.FromMinutes(2);


            string xml = $@"
<configuration> 
    <system.serviceModel>         
        <bindings>         
            <customBinding>
                <binding name=""{expectedName}""
                         openTimeout=""{expectedDefaultTimeout:g}""
                         closeTimeout=""{expectedDefaultTimeout:g}""
                         sendTimeout=""{expectedDefaultTimeout:g}""
                         receiveTimeout=""{expectedReceiveTimeout:g}"">
                </binding>
            </customBinding>                             
        </bindings>                             
   </system.serviceModel>
</configuration>";

            using (var fs = TemporaryFileStream.Create(xml))
            {
                using (ServiceProvider provider = CreateProvider(fs.Name))
                {
                    IConfigurationHolder settingHolder = GetConfigurationHolder(provider);

                    CustomBinding actualBinding = settingHolder.ResolveBinding(nameof(CustomBinding), expectedName) as CustomBinding;

                    Assert.Equal(expectedName, actualBinding.Name);
                    Assert.Equal(expectedDefaultTimeout, actualBinding.CloseTimeout);
                    Assert.Equal(expectedDefaultTimeout, actualBinding.OpenTimeout);
                    Assert.Equal(expectedDefaultTimeout, actualBinding.SendTimeout);
                    Assert.Equal(expectedReceiveTimeout, actualBinding.ReceiveTimeout);
                }
            }
        }

        [Fact]
        public void CustomBinding_WithDefaultSettings()
        {
            string expectedName = "customBinding";
            TimeSpan expectedReceiveTimeout = TimeSpan.FromMinutes(10);
            TimeSpan expectedDefaultTimeout = TimeSpan.FromMinutes(1);


            string xml = $@"
<configuration> 
    <system.serviceModel>         
        <bindings>         
            <customBinding>
                <binding name=""{expectedName}"">
                </binding>
            </customBinding>                             
        </bindings>                             
   </system.serviceModel>
</configuration>";

            using (var fs = TemporaryFileStream.Create(xml))
            {
                using (ServiceProvider provider = CreateProvider(fs.Name))
                {
                    IConfigurationHolder settingHolder = GetConfigurationHolder(provider);

                    CustomBinding actualBinding = settingHolder.ResolveBinding(nameof(CustomBinding), expectedName) as CustomBinding;

                    Assert.Equal(expectedName, actualBinding.Name);
                    Assert.Equal(expectedDefaultTimeout, actualBinding.CloseTimeout);
                    Assert.Equal(expectedDefaultTimeout, actualBinding.OpenTimeout);
                    Assert.Equal(expectedDefaultTimeout, actualBinding.SendTimeout);
                    Assert.Equal(expectedReceiveTimeout, actualBinding.ReceiveTimeout);
                }
            }
        }

        #endregion

        #region Encoding

        #region TextEncoding

        public static IEnumerable<object[]> GetEncodingForTest()
        {
            yield return new object[] { Encoding.BigEndianUnicode };
            yield return new object[] { Encoding.UTF8 };
            yield return new object[] { Encoding.Unicode };
        }

        [Theory]
        [MemberData(nameof(GetEncodingForTest))]
        public void CustomBinding_WithTextEncodingSettings(Encoding encoding)
        {
            string expectedName = "customBinding";
            int expectedMaxDepth = 2147483647;
            int expectedMaxReadPoolSize = 24;
            int expectedMaxWritePoolSize = 8;
            Encoding expectedEncoding = encoding;
            MessageVersion expectedMessageVersion = MessageVersion.Soap11;
            
            string xml = $@"
<configuration> 
    <system.serviceModel>         
        <bindings>         
            <customBinding>
                <binding name=""{expectedName}"">
                    <textMessageEncoding maxReadPoolSize=""{expectedMaxReadPoolSize}""
                                         maxWritePoolSize=""{expectedMaxWritePoolSize}""
                                         writeEncoding=""{expectedEncoding.HeaderName}""
                                         messageVersion=""Soap11"">
                        <readerQuotas maxDepth=""{expectedMaxDepth}"" />
                    </textMessageEncoding>
                </binding>
            </customBinding>                             
        </bindings>                             
   </system.serviceModel>
</configuration>";

            using (var fs = TemporaryFileStream.Create(xml))
            {
                using (ServiceProvider provider = CreateProvider(fs.Name))
                {
                    IConfigurationHolder settingHolder = GetConfigurationHolder(provider);

                    CustomBinding actualBinding = settingHolder.ResolveBinding(nameof(CustomBinding), expectedName) as CustomBinding;
                    TextMessageEncodingBindingElement actualMessageEncodingBindingElement = actualBinding.Elements.Find<TextMessageEncodingBindingElement>();

                    Assert.Equal(expectedName, actualBinding.Name);
                    Assert.Equal(expectedMaxReadPoolSize, actualMessageEncodingBindingElement.MaxReadPoolSize);
                    Assert.Equal(expectedMaxWritePoolSize, actualMessageEncodingBindingElement.MaxWritePoolSize);
                    Assert.Equal(expectedEncoding, actualMessageEncodingBindingElement.WriteEncoding);
                    Assert.Equal(expectedMaxDepth, actualMessageEncodingBindingElement.ReaderQuotas.MaxDepth);
                    Assert.Equal(expectedMessageVersion, actualMessageEncodingBindingElement.MessageVersion);
                }
            }
        }

        [Fact]
        public void CustomBinding_WithTextEncodingDefaultSettings()
        {
            string expectedName = "customBinding";
            int expectedMaxReadPoolSize = 64;
            int expectedMaxWritePoolSize = 16;
            Encoding expectedEncoding = Encoding.UTF8;
            MessageVersion expectedMessageVersion = MessageVersion.Soap12WSAddressing10;


            string xml = $@"
<configuration> 
    <system.serviceModel>         
        <bindings>         
            <customBinding>
                <binding name=""{expectedName}"">
                    <textMessageEncoding />
                </binding>
            </customBinding>                             
        </bindings>                             
   </system.serviceModel>
</configuration>";

            using (var fs = TemporaryFileStream.Create(xml))
            {
                using (ServiceProvider provider = CreateProvider(fs.Name))
                {
                    IConfigurationHolder settingHolder = GetConfigurationHolder(provider);

                    CustomBinding actualBinding = settingHolder.ResolveBinding(nameof(CustomBinding), expectedName) as CustomBinding;
                    TextMessageEncodingBindingElement actualMessageEncodingBindingElement = actualBinding.Elements.Find<TextMessageEncodingBindingElement>();

                    Assert.Equal(expectedName, actualBinding.Name);
                    Assert.Equal(expectedMaxReadPoolSize, actualMessageEncodingBindingElement.MaxReadPoolSize);
                    Assert.Equal(expectedMaxWritePoolSize, actualMessageEncodingBindingElement.MaxWritePoolSize);
                    Assert.Equal(expectedEncoding, actualMessageEncodingBindingElement.WriteEncoding);
                    Assert.Equal(expectedMessageVersion, actualMessageEncodingBindingElement.MessageVersion);
                }
            }
        }

        #endregion

        #region BinaryEncoding

        [Fact]
        public void CustomBinding_WithBinaryEncodingSettings()
        {
            string expectedName = "customBinding";
            int expectedMaxDepth = 2147483647;
            int expectedMaxReadPoolSize = 24;
            int expectedMaxWritePoolSize = 8;
            int expectedMaxSessionSize = 4096;
            CompressionFormat expectedCompressionFormat = CompressionFormat.GZip;


            string xml = $@"
<configuration> 
    <system.serviceModel>         
        <bindings>         
            <customBinding>
                <binding name=""{expectedName}"">
                    <binaryMessageEncoding maxReadPoolSize=""{expectedMaxReadPoolSize}""
                                           maxWritePoolSize=""{expectedMaxWritePoolSize}""
                                           maxSessionSize=""{expectedMaxSessionSize}""
                                           compressionFormat=""{expectedCompressionFormat}"">
                        <readerQuotas maxDepth=""{expectedMaxDepth}"" />
                    </binaryMessageEncoding>
                </binding>
            </customBinding>                             
        </bindings>                             
   </system.serviceModel>
</configuration>";

            using (var fs = TemporaryFileStream.Create(xml))
            {
                using (ServiceProvider provider = CreateProvider(fs.Name))
                {
                    IConfigurationHolder settingHolder = GetConfigurationHolder(provider);

                    CustomBinding actualBinding = settingHolder.ResolveBinding(nameof(CustomBinding), expectedName) as CustomBinding;
                    BinaryMessageEncodingBindingElement actualMessageEncodingBindingElement = actualBinding.Elements.Find<BinaryMessageEncodingBindingElement>();

                    Assert.Equal(expectedName, actualBinding.Name);
                    Assert.Equal(expectedMaxReadPoolSize, actualMessageEncodingBindingElement.MaxReadPoolSize);
                    Assert.Equal(expectedMaxWritePoolSize, actualMessageEncodingBindingElement.MaxWritePoolSize);
                    Assert.Equal(expectedCompressionFormat, actualMessageEncodingBindingElement.CompressionFormat);
                    Assert.Equal(expectedMaxSessionSize, actualMessageEncodingBindingElement.MaxSessionSize);
                    Assert.Equal(expectedMaxDepth, actualMessageEncodingBindingElement.ReaderQuotas.MaxDepth);
                }
            }
        }

        [Fact]
        public void CustomBinding_WithBinaryEncodingDefaultSettings()
        {
            string expectedName = "customBinding";
            int expectedMaxReadPoolSize = 64;
            int expectedMaxWritePoolSize = 16;
            int expectedMaxSessionSize = 2048;
            CompressionFormat expectedCompressionFormat = CompressionFormat.None;


            string xml = $@"
<configuration> 
    <system.serviceModel>         
        <bindings>         
            <customBinding>
                <binding name=""{expectedName}"">
                    <binaryMessageEncoding />
                </binding>
            </customBinding>                             
        </bindings>                             
   </system.serviceModel>
</configuration>";

            using (var fs = TemporaryFileStream.Create(xml))
            {
                using (ServiceProvider provider = CreateProvider(fs.Name))
                {
                    IConfigurationHolder settingHolder = GetConfigurationHolder(provider);

                    CustomBinding actualBinding = settingHolder.ResolveBinding(nameof(CustomBinding), expectedName) as CustomBinding;
                    BinaryMessageEncodingBindingElement actualMessageEncodingBindingElement = actualBinding.Elements.Find<BinaryMessageEncodingBindingElement>();

                    Assert.Equal(expectedName, actualBinding.Name);
                    Assert.Equal(expectedMaxReadPoolSize, actualMessageEncodingBindingElement.MaxReadPoolSize);
                    Assert.Equal(expectedMaxWritePoolSize, actualMessageEncodingBindingElement.MaxWritePoolSize);
                    Assert.Equal(expectedCompressionFormat, actualMessageEncodingBindingElement.CompressionFormat);
                    Assert.Equal(expectedMaxSessionSize, actualMessageEncodingBindingElement.MaxSessionSize);
                }
            }
        }

        #endregion

        #region MtomEncoding

        [Fact]
        public void CustomBinding_WithMtomEncodingSettings()
        {
            string expectedName = "customBinding";
            int expectedMaxDepth = 2147483647;
            int expectedMaxReadPoolSize = 24;
            int expectedMaxWritePoolSize = 8;
            Encoding expectedEncoding = Encoding.Unicode;
            MessageVersion expectedMessageVersion = MessageVersion.Soap11;


            string xml = $@"
<configuration> 
    <system.serviceModel>         
        <bindings>         
            <customBinding>
                <binding name=""{expectedName}"">
                    <mtomMessageEncoding maxReadPoolSize=""{expectedMaxReadPoolSize}""
                                         maxWritePoolSize=""{expectedMaxWritePoolSize}""
                                         writeEncoding=""utf-16LE""
                                         messageVersion=""Soap11"">
                        <readerQuotas maxDepth=""{expectedMaxDepth}"" />
                    </mtomMessageEncoding>
                </binding>
            </customBinding>                             
        </bindings>                             
   </system.serviceModel>
</configuration>";

            using (var fs = TemporaryFileStream.Create(xml))
            {
                using (ServiceProvider provider = CreateProvider(fs.Name))
                {
                    IConfigurationHolder settingHolder = GetConfigurationHolder(provider);

                    CustomBinding actualBinding = settingHolder.ResolveBinding(nameof(CustomBinding), expectedName) as CustomBinding;
                    MtomMessageEncodingBindingElement actualMessageEncodingBindingElement = actualBinding.Elements.Find<MtomMessageEncodingBindingElement>();

                    Assert.Equal(expectedName, actualBinding.Name);
                    Assert.Equal(expectedMaxReadPoolSize, actualMessageEncodingBindingElement.MaxReadPoolSize);
                    Assert.Equal(expectedMaxWritePoolSize, actualMessageEncodingBindingElement.MaxWritePoolSize);
                    Assert.Equal(expectedEncoding, actualMessageEncodingBindingElement.WriteEncoding);
                    Assert.Equal(expectedMessageVersion, actualMessageEncodingBindingElement.MessageVersion);
                    Assert.Equal(expectedMaxDepth, actualMessageEncodingBindingElement.ReaderQuotas.MaxDepth);
                }
            }
        }

        [Fact]
        public void CustomBinding_WithMtomEncodingDefaultSettings()
        {
            string expectedName = "customBinding";
            int expectedMaxReadPoolSize = 64;
            int expectedMaxWritePoolSize = 16;
            Encoding expectedEncoding = Encoding.UTF8;
            MessageVersion expectedMessageVersion = MessageVersion.Soap12WSAddressing10;


            string xml = $@"
<configuration> 
    <system.serviceModel>         
        <bindings>         
            <customBinding>
                <binding name=""{expectedName}"">
                    <mtomMessageEncoding />
                </binding>
            </customBinding>                             
        </bindings>                             
   </system.serviceModel>
</configuration>";

            using (var fs = TemporaryFileStream.Create(xml))
            {
                using (ServiceProvider provider = CreateProvider(fs.Name))
                {
                    IConfigurationHolder settingHolder = GetConfigurationHolder(provider);

                    CustomBinding actualBinding = settingHolder.ResolveBinding(nameof(CustomBinding), expectedName) as CustomBinding;
                    MtomMessageEncodingBindingElement actualMessageEncodingBindingElement = actualBinding.Elements.Find<MtomMessageEncodingBindingElement>();

                    Assert.Equal(expectedName, actualBinding.Name);
                    Assert.Equal(expectedMaxReadPoolSize, actualMessageEncodingBindingElement.MaxReadPoolSize);
                    Assert.Equal(expectedMaxWritePoolSize, actualMessageEncodingBindingElement.MaxWritePoolSize);
                    Assert.Equal(expectedMessageVersion, actualMessageEncodingBindingElement.MessageVersion);
                    Assert.Equal(expectedEncoding, actualMessageEncodingBindingElement.WriteEncoding);
                }
            }
        }

        #endregion

        #endregion

        #region Transport

        #region HttpTransport

        [Fact]
        public void CustomBinding_WithHttpTransportSettings()
        {
            string expectedName = "customBinding";
            long expectedMaxReceivedMessageSize = 1073741824;
            long expectedMaxBufferSize = 1073741824;
            string expectedScheme = "http";


            string xml = $@"
<configuration> 
    <system.serviceModel>         
        <bindings>         
            <customBinding>
                <binding name=""{expectedName}"">
                    <httpTransport maxReceivedMessageSize=""{expectedMaxReceivedMessageSize}""
                                   maxBufferSize=""{expectedMaxBufferSize}""/>
                </binding>
            </customBinding>                             
        </bindings>                             
   </system.serviceModel>
</configuration>";

            using (var fs = TemporaryFileStream.Create(xml))
            {
                using (ServiceProvider provider = CreateProvider(fs.Name))
                {
                    IConfigurationHolder settingHolder = GetConfigurationHolder(provider);

                    CustomBinding actualBinding = settingHolder.ResolveBinding(nameof(CustomBinding), expectedName) as CustomBinding;
                    HttpTransportBindingElement actualTransportElement = actualBinding.Elements.Find<HttpTransportBindingElement>();

                    Assert.Equal(expectedName, actualBinding.Name);
                    Assert.Equal(expectedMaxReceivedMessageSize, actualTransportElement.MaxReceivedMessageSize);
                    Assert.Equal(expectedMaxBufferSize, actualTransportElement.MaxBufferSize);
                    Assert.Equal(expectedScheme, actualBinding.Scheme);
                    Assert.Equal(TransferMode.Buffered, actualTransportElement.TransferMode);
                }
            }
        }

        [Fact]
        public void CustomBinding_WithHttpTransportDefaultSettings()
        {
            string expectedName = "customBinding";
            long expectedMaxReceivedMessageSize = 65536;
            long expectedMaxBufferSize = 65536;
            string expectedScheme = "http";


            string xml = $@"
<configuration> 
    <system.serviceModel>         
        <bindings>         
            <customBinding>
                <binding name=""{expectedName}"">
                    <httpTransport />
                </binding>
            </customBinding>                             
        </bindings>                             
   </system.serviceModel>
</configuration>";

            using (var fs = TemporaryFileStream.Create(xml))
            {
                using (ServiceProvider provider = CreateProvider(fs.Name))
                {
                    IConfigurationHolder settingHolder = GetConfigurationHolder(provider);

                    CustomBinding actualBinding = settingHolder.ResolveBinding(nameof(CustomBinding), expectedName) as CustomBinding;
                    HttpTransportBindingElement actualTransportElement = actualBinding.Elements.Find<HttpTransportBindingElement>();

                    Assert.Equal(expectedName, actualBinding.Name);
                    Assert.Equal(expectedMaxReceivedMessageSize, actualTransportElement.MaxReceivedMessageSize);
                    Assert.Equal(expectedMaxBufferSize, actualTransportElement.MaxBufferSize);
                    Assert.Equal(expectedScheme, actualBinding.Scheme);
                    Assert.Equal(TransferMode.Buffered, actualTransportElement.TransferMode);
                }
            }
        }

        #endregion

        #region HttpsTransport

        [Fact]
        public void CustomBinding_WithHttpsTransportSettings()
        {
            string expectedName = "customBinding";
            long expectedMaxReceivedMessageSize = 1073741824;
            long expectedMaxBufferSize = 1073741824;
            string expectedScheme = "https";
            bool expectedRequireClientCertificate = true;


            string xml = $@"
<configuration> 
    <system.serviceModel>         
        <bindings>         
            <customBinding>
                <binding name=""{expectedName}"">
                    <httpsTransport maxReceivedMessageSize=""{expectedMaxReceivedMessageSize}""
                                    maxBufferSize=""{expectedMaxBufferSize}""
                                    requireClientCertificate=""{expectedRequireClientCertificate}""/>
                </binding>
            </customBinding>                             
        </bindings>                             
   </system.serviceModel>
</configuration>";

            using (var fs = TemporaryFileStream.Create(xml))
            {
                using (ServiceProvider provider = CreateProvider(fs.Name))
                {
                    IConfigurationHolder settingHolder = GetConfigurationHolder(provider);

                    CustomBinding actualBinding = settingHolder.ResolveBinding(nameof(CustomBinding), expectedName) as CustomBinding;
                    HttpsTransportBindingElement actualTransportElement = actualBinding.Elements.Find<HttpsTransportBindingElement>();

                    Assert.Equal(expectedName, actualBinding.Name);
                    Assert.Equal(expectedMaxReceivedMessageSize, actualTransportElement.MaxReceivedMessageSize);
                    Assert.Equal(expectedMaxBufferSize, actualTransportElement.MaxBufferSize);
                    Assert.Equal(expectedScheme, actualBinding.Scheme);
                    Assert.Equal(expectedRequireClientCertificate, actualTransportElement.RequireClientCertificate);
                    Assert.Equal(TransferMode.Buffered, actualTransportElement.TransferMode);
                }
            }
        }

        [Fact]
        public void CustomBinding_WithHttpsTransportDefaultSettings()
        {
            string expectedName = "customBinding";
            long expectedMaxReceivedMessageSize = 65536;
            long expectedMaxBufferSize = 65536;
            string expectedScheme = "https";
            bool expectedRequireClientCertificate = false;


            string xml = $@"
<configuration> 
    <system.serviceModel>         
        <bindings>         
            <customBinding>
                <binding name=""{expectedName}"">
                    <httpsTransport />
                </binding>
            </customBinding>                             
        </bindings>                             
   </system.serviceModel>
</configuration>";

            using (var fs = TemporaryFileStream.Create(xml))
            {
                using (ServiceProvider provider = CreateProvider(fs.Name))
                {
                    IConfigurationHolder settingHolder = GetConfigurationHolder(provider);

                    CustomBinding actualBinding = settingHolder.ResolveBinding(nameof(CustomBinding), expectedName) as CustomBinding;
                    HttpsTransportBindingElement actualTransportElement = actualBinding.Elements.Find<HttpsTransportBindingElement>();

                    Assert.Equal(expectedName, actualBinding.Name);
                    Assert.Equal(expectedMaxReceivedMessageSize, actualTransportElement.MaxReceivedMessageSize);
                    Assert.Equal(expectedMaxBufferSize, actualTransportElement.MaxBufferSize);
                    Assert.Equal(expectedScheme, actualBinding.Scheme);
                    Assert.Equal(expectedRequireClientCertificate, actualTransportElement.RequireClientCertificate);
                    Assert.Equal(TransferMode.Buffered, actualTransportElement.TransferMode);
                }
            }
        }

        #endregion

        #region TcpTransport

        [Fact]
        public void CustomBinding_WithTcpTransportSettings()
        {
            string expectedName = "customBinding";
            long expectedMaxReceivedMessageSize = 1073741824;
            long expectedMaxBufferSize = 1073741824;
            string expectedScheme = "net.tcp";
            int expectedConnectionBufferSize = 8192 * 2;
            HostNameComparisonMode expectedHostNameComparisonMode = HostNameComparisonMode.Exact;
            TimeSpan expectedChannelInitializationTimeout = TimeSpan.FromSeconds(20);
            int expectedMaxPendingConnections = 96 * 2;
            TimeSpan expectedMaxOutputDelay = TimeSpan.FromMilliseconds(300);
            int expectedMaxPendingAccepts = 16 * 2;
            TransferMode expectedTransferMode = TransferMode.Streamed;
            int expectedListenBacklog = 96 * 2;
            string expectedServiceName = "expectedServiceName";
            PolicyEnforcement expectedPolicyEnforcement = PolicyEnforcement.Always;
            ProtectionScenario expectedProtectionScenario = ProtectionScenario.TrustedProxy;
            TimeSpan expectedIdleTimeout = TimeSpan.FromMinutes(4);
            int expectedMaxOutboundConnectionsPerEndpoint = 20;

            string xml = $@"
<configuration> 
    <system.serviceModel>         
        <bindings>         
            <customBinding>
                <binding name=""{expectedName}"">
                    <tcpTransport maxReceivedMessageSize=""{expectedMaxReceivedMessageSize}""
                                  maxBufferSize=""{expectedMaxBufferSize}""
                                  connectionBufferSize=""{expectedConnectionBufferSize}""
                                  hostNameComparisonMode=""{expectedHostNameComparisonMode}""
                                  channelInitializationTimeout=""{expectedChannelInitializationTimeout:g}""
                                  maxPendingConnections=""{expectedMaxPendingConnections}""
                                  maxOutputDelay=""{expectedMaxOutputDelay:g}""
                                  maxPendingAccepts=""{expectedMaxPendingAccepts}""
                                  transferMode=""{expectedTransferMode}""
                                  listenBacklog=""{expectedListenBacklog}"">
                        <connectionPoolSettings
                                                idleTimeout=""{expectedIdleTimeout:g}""
                                                maxOutboundConnectionsPerEndpoint=""{expectedMaxOutboundConnectionsPerEndpoint}""/>
                        <extendedProtectionPolicy policyEnforcement=""{expectedPolicyEnforcement}""
                                                  protectionScenario=""{expectedProtectionScenario}"">
                            <customServiceNames>
                                <add name=""{expectedServiceName}""/>
                            </customServiceNames>
                        </extendedProtectionPolicy>
                    </tcpTransport>
                </binding>
            </customBinding>                             
        </bindings>                             
   </system.serviceModel>
</configuration>";

            using (var fs = TemporaryFileStream.Create(xml))
            {
                using (ServiceProvider provider = CreateProvider(fs.Name))
                {
                    IConfigurationHolder settingHolder = GetConfigurationHolder(provider);

                    CustomBinding actualBinding = settingHolder.ResolveBinding(nameof(CustomBinding), expectedName) as CustomBinding;
                    TcpTransportBindingElement actualTransportElement = actualBinding.Elements.Find<TcpTransportBindingElement>();
                    
                    Assert.Equal(expectedName, actualBinding.Name);
                    Assert.Equal(expectedMaxReceivedMessageSize, actualTransportElement.MaxReceivedMessageSize);
                    Assert.Equal(expectedMaxBufferSize, actualTransportElement.MaxBufferSize);
                    Assert.Equal(expectedScheme, actualBinding.Scheme);
                    Assert.Equal(expectedConnectionBufferSize, actualTransportElement.ConnectionBufferSize);
                    Assert.Equal(expectedHostNameComparisonMode, actualTransportElement.HostNameComparisonMode);
                    Assert.Equal(expectedChannelInitializationTimeout, actualTransportElement.ChannelInitializationTimeout);
                    Assert.Equal(expectedMaxPendingConnections, actualTransportElement.MaxPendingConnections);
                    Assert.Equal(expectedMaxOutputDelay, actualTransportElement.MaxOutputDelay);
                    Assert.Equal(expectedMaxPendingAccepts, actualTransportElement.MaxPendingAccepts);
                    Assert.Equal(expectedTransferMode, actualTransportElement.TransferMode);
                    Assert.Equal(expectedListenBacklog, actualTransportElement.ListenBacklog);
                    Assert.Equal(expectedPolicyEnforcement, actualTransportElement.ExtendedProtectionPolicy.PolicyEnforcement);
                    Assert.Equal(expectedProtectionScenario, actualTransportElement.ExtendedProtectionPolicy.ProtectionScenario);
                    Assert.True(actualTransportElement.ExtendedProtectionPolicy.CustomServiceNames.Contains(expectedServiceName));
                    Assert.Equal(expectedIdleTimeout, actualTransportElement.ConnectionPoolSettings.IdleTimeout);
                    Assert.Equal(expectedMaxOutboundConnectionsPerEndpoint, actualTransportElement.ConnectionPoolSettings.MaxOutboundConnectionsPerEndpoint);
                }
            }
        }

        [Fact]
        public void CustomBinding_WithTcpTransportDefaultSettings()
        {
            string expectedName = "customBinding";
            long expectedMaxReceivedMessageSize = 65536;
            long expectedMaxBufferSize = 65536;
            string expectedScheme = "net.tcp";
            int expectedConnectionBufferSize = 8192;
            HostNameComparisonMode expectedHostNameComparisonMode = HostNameComparisonMode.StrongWildcard;
            TimeSpan expectedChannelInitializationTimeout = TimeSpan.FromSeconds(30);
            TimeSpan expectedMaxOutputDelay = TimeSpan.FromMilliseconds(200);
            TransferMode expectedTransferMode = TransferMode.Buffered;
            PolicyEnforcement expectedPolicyEnforcement = PolicyEnforcement.Never;

            string xml = $@"
<configuration> 
    <system.serviceModel>         
        <bindings>         
            <customBinding>
                <binding name=""{expectedName}"">
                    <tcpTransport />
                </binding>
            </customBinding>                             
        </bindings>                             
   </system.serviceModel>
</configuration>";

            using (var fs = TemporaryFileStream.Create(xml))
            {
                using (ServiceProvider provider = CreateProvider(fs.Name))
                {
                    IConfigurationHolder settingHolder = GetConfigurationHolder(provider);

                    CustomBinding actualBinding = settingHolder.ResolveBinding(nameof(CustomBinding), expectedName) as CustomBinding;
                    TcpTransportBindingElement actualTransportElement = actualBinding.Elements.Find<TcpTransportBindingElement>();

                    Assert.Equal(expectedName, actualBinding.Name);
                    Assert.Equal(expectedMaxReceivedMessageSize, actualTransportElement.MaxReceivedMessageSize);
                    Assert.Equal(expectedMaxBufferSize, actualTransportElement.MaxBufferSize);
                    Assert.Equal(expectedScheme, actualBinding.Scheme);
                    Assert.Equal(expectedConnectionBufferSize, actualTransportElement.ConnectionBufferSize);
                    Assert.Equal(expectedHostNameComparisonMode, actualTransportElement.HostNameComparisonMode);
                    Assert.Equal(expectedChannelInitializationTimeout, actualTransportElement.ChannelInitializationTimeout);
                    Assert.Equal(expectedMaxOutputDelay, actualTransportElement.MaxOutputDelay);
                    Assert.Equal(expectedTransferMode, actualTransportElement.TransferMode);
                    Assert.Equal(expectedPolicyEnforcement, actualTransportElement.ExtendedProtectionPolicy.PolicyEnforcement);

                }
            }

        }

        #endregion

        #endregion

        #region Security

        [Fact]
        public void CustomBinding_WithSecurity()
        {
            string expectedName = "customBinding";
            SecurityAlgorithmSuite expectedSecurityAlgorithmSuite = SecurityAlgorithmSuite.Basic256Rsa15;
            bool expectedEnableUnsecuredResponse = true;
            bool expectedRequireDerivedKeys = true;
            AuthenticationMode expectedAuthenticationMode = AuthenticationMode.SecureConversation;


            string xml = $@"
<configuration> 
    <system.serviceModel>         
        <bindings>         
            <customBinding>
                <binding name=""{expectedName}"">
                    <security defaultAlgorithmSuite=""Basic256Rsa15""
                                enableUnsecuredResponse=""{expectedEnableUnsecuredResponse}"" authenticationMode=""{expectedAuthenticationMode}""
                                requireDerivedKeys=""{expectedRequireDerivedKeys}"" securityHeaderLayout=""Lax"" includeTimestamp=""false""
                                allowInsecureTransport=""true"" keyEntropyMode=""ServerEntropy""
                                messageProtectionOrder=""SignBeforeEncrypt"" protectTokens=""true""
                                messageSecurityVersion=""WSSecurity11WSTrustFebruary2005WSSecureConversationFebruary2005WSSecurityPolicy11BasicSecurityProfile10""
                                requireSecurityContextCancellation=""false"" requireSignatureConfirmation=""true""
                                canRenewSecurityContextToken=""false"" >
                        <localServiceSettings detectReplays=""false"" issuedCookieLifetime=""10:10:00""
                                maxStatefulNegotiations=""256"" replayCacheSize=""800000"" maxClockSkew=""00:06:00""
                                negotiationTimeout=""00:02:00"" replayWindow=""00:06:00"" inactivityTimeout=""00:03:00""
                                sessionKeyRenewalInterval=""15:10:00"" sessionKeyRolloverInterval=""00:06:00""
                                reconnectTransportOnFailure=""false"" maxPendingSessions=""256""
                                maxCachedCookies=""2000"" timestampValidityDuration=""00:06:00"" />  
                    </security>  
                  </binding>
            </customBinding>                             
        </bindings>                             
   </system.serviceModel>
</configuration>";

            using (var fs = TemporaryFileStream.Create(xml))
            {
                using (ServiceProvider provider = CreateProvider(fs.Name))
                {
                    IConfigurationHolder settingHolder = GetConfigurationHolder(provider);

                    CustomBinding actualBinding = settingHolder.ResolveBinding(nameof(CustomBinding), expectedName) as CustomBinding;
                    SecurityBindingElement actualSecurityBindingElement = actualBinding.Elements.Find<SecurityBindingElement>();

                    Assert.NotNull(actualSecurityBindingElement);
                    Assert.NotNull(actualSecurityBindingElement.LocalServiceSettings);
                    Assert.Equal(expectedSecurityAlgorithmSuite, actualSecurityBindingElement.DefaultAlgorithmSuite);
                    Assert.Equal(expectedEnableUnsecuredResponse,actualSecurityBindingElement.EnableUnsecuredResponse);
                    Assert.NotNull(actualSecurityBindingElement.EndpointSupportingTokenParameters.Endorsing.First() as SecureConversationSecurityTokenParameters);
                    

                    Assert.Equal(expectedName, actualBinding.Name);
                }
            }
        }

        #endregion
    }
}
