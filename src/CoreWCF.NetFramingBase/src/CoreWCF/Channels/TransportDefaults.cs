// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Security.Authentication;
using System.Text;
using CoreWCF.Security;

namespace CoreWCF.Channels
{
    internal static class TransportDefaults
    {
        internal const bool ExtractGroupsForWindowsAccounts = true; //SspiSecurityTokenProvider.DefaultExtractWindowsGroupClaims;
        internal const long MaxReceivedMessageSize = 65536;
        //internal const HostNameComparisonMode HostNameComparisonMode = CoreWCF.HostNameComparisonMode.Exact;
        internal const int MaxDrainSize = (int)MaxReceivedMessageSize;
        //internal const long MaxBufferPoolSize = 512 * 1024;
        internal const int MaxBufferSize = (int)MaxReceivedMessageSize;
        internal const bool RequireClientCertificate = false;
        internal const SslProtocols SslProtocols = System.Security.Authentication.SslProtocols.None; // Let the OS decide

        //internal static MessageEncoderFactory GetDefaultMessageEncoderFactory()
        //{
        //    return new BinaryMessageEncodingBindingElement().CreateMessageEncoderFactory();
        //}
    }

    internal static class ConnectionOrientedTransportDefaults
    {
        internal const bool AllowNtlm = true; // Formerly SspiSecurityTokenProvider.DefaultAllowNtlm
        internal const HostNameComparisonMode HostNameComparisonMode = CoreWCF.HostNameComparisonMode.StrongWildcard;
        internal const int ConnectionBufferSize = 8192;
        internal const int MaxContentTypeSize = 256;
        internal const int MaxOutboundConnectionsPerEndpoint = 10;
        internal const int MaxViaSize = 2048;
        internal const ProtectionLevel ProtectionLevel = System.Net.Security.ProtectionLevel.EncryptAndSign;
        internal const TransferMode TransferMode = CoreWCF.TransferMode.Buffered;

        internal static TimeSpan IdleTimeout => TimeSpan.FromMinutes(2);
        internal static TimeSpan ChannelInitializationTimeout => TimeSpan.FromSeconds(30);
        internal static TimeSpan MaxOutputDelay => TimeSpan.FromMilliseconds(200);
        internal static int GetMaxConnections() => GetMaxPendingConnections();
        internal static int GetMaxPendingConnections() => 12 * Environment.ProcessorCount;
        internal static int GetMaxPendingAccepts() => 2 * Environment.ProcessorCount;
    }
}
