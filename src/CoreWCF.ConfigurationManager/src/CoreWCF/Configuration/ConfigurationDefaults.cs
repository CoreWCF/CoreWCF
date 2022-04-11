// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net;
using System.Net.Security;
using System.Net.WebSockets;
using System.Security.Authentication;
using System.Text;
using CoreWCF.Channels;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Security;

namespace CoreWCF.Configuration
{
    internal static class BinaryEncoderDefaults
    {
        internal static EnvelopeVersion EnvelopeVersion { get { return EnvelopeVersion.Soap12; } }

        internal const int MaxSessionSize = 2048;
    }

    internal static class ConnectionOrientedTransportDefaults
    {
        internal const HostNameComparisonMode HostNameComparisonMode = CoreWCF.HostNameComparisonMode.StrongWildcard;
        internal const int ConnectionBufferSize = 8192;
        internal const ProtectionLevel ProtectionLevel = System.Net.Security.ProtectionLevel.EncryptAndSign;
        internal const string ChannelInitializationTimeoutString = "00:00:30";
        internal const int MaxPendingConnectionsConst = 0;
        internal const string MaxOutputDelayString = "00:00:00.2";
        internal const int MaxPendingAcceptsConst = 0;
        internal const TransferMode TransferMode = CoreWCF.TransferMode.Buffered;
        internal const string IdleTimeoutString = "00:02:00";
        internal const int MaxOutboundConnectionsPerEndpoint = 10;
    }

    internal static class HttpTransportDefaults
    {
        internal const bool AllowCookies = false;
        internal const AuthenticationSchemes AuthenticationScheme = AuthenticationSchemes.Anonymous;
        internal const bool BypassProxyOnLocal = false;
        internal const bool DecompressionEnabled = true;
        internal const HostNameComparisonMode HostNameComparisonMode = CoreWCF.HostNameComparisonMode.StrongWildcard;
        internal const bool KeepAliveEnabled = true;
        internal const Uri ProxyAddress = null;
        internal const AuthenticationSchemes ProxyAuthenticationScheme = AuthenticationSchemes.Anonymous;
        internal const string Realm = "";
        internal const TransferMode TransferMode = CoreWCF.TransferMode.Buffered;
        internal const bool UnsafeConnectionNtlmAuthentication = false;
        internal const bool UseDefaultWebProxy = true;
        
        internal const string RequestInitializationTimeoutString = "00:00:00";
    }

    internal static class MtomEncoderDefaults
    {
        internal const int MaxBufferSize = 65536;
    }

    internal static class OneWayDefaults
    {
        internal const string IdleTimeoutString = "00:02:00";
        internal const int MaxOutboundChannelsPerEndpoint = 10;
        internal const string LeaseTimeoutString = "00:10:00";
        internal const int MaxAcceptedChannels = 10;
        internal const bool PacketRoutable = false;
    }

    internal static class SecurityBindingDefaults
    {
        internal const SecurityKeyType DefaultKeyType = SecurityKeyType.SymmetricKey;
        internal const string DefaultAlgorithmSuiteString = "Default";
        internal const bool DefaultRequireDerivedKeys = true;
        internal const bool DefaultAllowSerializedSigningTokenOnReply = false;
        internal const bool DefaultEnableUnsecuredResponse = false;
        internal const bool DefaultIncludeTimestamp = true;
        internal const bool DefaultAllowInsecureTransport = false;
        internal const bool DefaultRequireSignatureConfirmation = false;
        internal const bool DefaultRequireCancellation = true;
        internal const bool DefaultCanRenewSession = true;
        internal const bool DefaultDetectReplays = true;
        internal const int DefaultMaxCachedNonces = 900000;
        internal const string DefaultTimestampValidityDurationString = "00:05:00";
        internal const string DefaultMaxClockSkewString = "00:05:00";
        internal const string DefaultReplayWindowString = "00:05:00";
        internal const string DefaultClientMaxTokenCachingTimeString = "10675199.02:48:05.4775807";
        internal const bool DefaultClientCacheTokens = true;
        internal const int DefaultServiceTokenValidityThresholdPercentage = 60;
        internal const string DefaultClientKeyRenewalIntervalString = "10:00:00";
        internal const string DefaultClientKeyRolloverIntervalString = "00:05:00";
        internal const bool DefaultTolerateTransportFailures = true;
        internal const string DefaultServerMaxNegotiationLifetimeString = "00:01:00";
        internal const string DefaultServerIssuedTokenLifetimeString = "10:00:00";
        internal const string DefaultServerIssuedTransitionTokenLifetimeString = "00:15:00";
        internal const int DefaultServerMaxActiveNegotiations = 128;
        internal const string DefaultKeyRenewalIntervalString = "15:00:00";
        internal const string DefaultKeyRolloverIntervalString = "00:05:00";
        internal const string DefaultInactivityTimeoutString = "00:02:00";
        internal const int DefaultMaximumPendingSessions = 128;
        internal const int DefaultServerMaxCachedTokens = 1000;
        internal const SecurityHeaderLayout DefaultSecurityHeaderLayout = SecurityHeaderLayout.Strict;
        internal const SecurityKeyEntropyMode DefaultKeyEntropyMode = SecurityKeyEntropyMode.CombinedEntropy;
        internal const MessageProtectionOrder DefaultMessageProtectionOrder = MessageProtectionOrder.SignBeforeEncryptAndEncryptSignature;
        internal const AuthenticationMode DefaultAuthenticationMode = AuthenticationMode.SspiNegotiatedOverTransport;
    }

    internal static class TcpTransportDefaults
    {
        internal const int ListenBacklogConst = 0;
        internal const string ConnectionLeaseTimeoutString = "00:05:00";
        internal const bool PortSharingEnabled = false;
        internal const bool TeredoEnabled = false;
    }

    internal static class TextEncoderDefaults
    {
        internal static readonly Encoding s_encoding = Encoding.GetEncoding(TextEncoderDefaults.EncodingString, new EncoderExceptionFallback(), new DecoderExceptionFallback());
        internal const string EncodingString = "utf-8";
        internal static readonly Encoding[] s_supportedEncodings = new Encoding[] { Encoding.UTF8, Encoding.Unicode, Encoding.BigEndianUnicode };
        internal const string MessageVersionString = ConfigurationStrings.Soap12WsAddressing10;
    }

    internal static class TransportDefaults
    {
        internal const long MaxReceivedMessageSize = 65536;
        internal const int MaxBufferSize = (int)MaxReceivedMessageSize;
        internal const bool RequireClientCertificate = false;
        internal const long MaxBufferPoolSize = 512 * 1024;
        internal const SslProtocols OldDefaultSslProtocols = SslProtocols.Tls |
                                                             SslProtocols.Tls11 |
                                                             SslProtocols.Tls12;
    }

    internal static class WebSocketDefaults
    {
        internal const WebSocketTransportUsage TransportUsage = WebSocketTransportUsage.Never;
        internal const bool CreateNotificationOnConnection = false;
        internal const string DefaultKeepAliveIntervalString = "00:00:00";

        internal const int BufferSize = 16 * 1024;
        internal const int MinReceiveBufferSize = 256;
        internal const int MinSendBufferSize = 16;
        internal const bool DisablePayloadMasking = false;
        internal const WebSocketMessageType DefaultWebSocketMessageType = WebSocketMessageType.Binary;
        internal const string SubProtocol = null;

        internal const int DefaultMaxPendingConnections = 0;
        // We set this number larger than that in TCP transport because in WebSocket cases, the connection is already authenticated
        // after we create the half-open channel. The default value is set as the default one as MaxConcurrentSessions to make it work
        // well in burst scenarios.
        internal const int DefaultMaxConcurrentSessions = 100;
        internal static readonly int s_maxPendingConnectionsCpuCount = DefaultMaxConcurrentSessions * Environment.ProcessorCount;

        internal const string WebSocketConnectionHeaderValue = "Upgrade";
        internal const string WebSocketUpgradeHeaderValue = "websocket";
    }

  
}
