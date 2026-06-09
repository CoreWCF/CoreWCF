// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net;
using System.Net.WebSockets;

namespace CoreWCF.Channels
{
    internal static class TransportDefaults
    {
        internal const long MaxReceivedMessageSize = 65536;
        internal const int MaxBufferSize = (int)MaxReceivedMessageSize;
        internal const bool RequireClientCertificate = false;
    }

    public static class BasicHttpBindingDefaults
    {
        public const WSMessageEncoding MessageEncoding = WSMessageEncoding.Text;
    }

    internal static class HttpTransportDefaults
    {
        internal const AuthenticationSchemes AuthenticationScheme = AuthenticationSchemes.Anonymous;
        internal const TransferMode TransferMode = CoreWCF.TransferMode.Buffered;
        internal const bool KeepAliveEnabled = true;
        internal const string Realm = "";

        internal static WebSocketTransportSettings GetDefaultWebSocketTransportSettings()
        {
            return new WebSocketTransportSettings();
        }
    }

    internal static class ConnectionOrientedTransportDefaults
    {
        internal const int ConnectionBufferSize = 8192;
    }

    internal static class WebSocketDefaults
    {
        internal const WebSocketTransportUsage TransportUsage = WebSocketTransportUsage.Never;
        internal const bool CreateNotificationOnConnection = false;
        internal static readonly TimeSpan DefaultKeepAliveInterval = TimeSpan.FromSeconds(0);

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
        internal static readonly int MaxPendingConnectionsCpuCount = DefaultMaxConcurrentSessions * Environment.ProcessorCount;

        internal const string WebSocketConnectionHeaderValue = "Upgrade";
        internal const string WebSocketUpgradeHeaderValue = "websocket";
    }

    internal static class ReliableSessionDefaults
    {
        internal const bool Ordered = true;
    }


    internal static class NetHttpBindingDefaults
    {
        internal const NetHttpMessageEncoding MessageEncoding = NetHttpMessageEncoding.Binary;
        internal const WebSocketTransportUsage TransportUsage = WebSocketTransportUsage.WhenDuplex;
    }
}
