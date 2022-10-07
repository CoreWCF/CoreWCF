// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net;

namespace CoreWCF.Channels
{
    internal class HttpTransportSettings : IHttpTransportFactorySettings
    {
        public TimeSpan CloseTimeout { get; set; }
        public TimeSpan OpenTimeout { get; set; }
        public TimeSpan ReceiveTimeout { get; set; }
        public TimeSpan SendTimeout { get; set; }
        public bool ManualAddressing { get; set; }
        public BufferManager BufferManager { get; set; }
        public long MaxReceivedMessageSize { get; set; }
        public MessageEncoderFactory MessageEncoderFactory { get; set; }
        public MessageVersion MessageVersion => MessageEncoderFactory.MessageVersion;
        public int MaxBufferSize { get; set; }
        public TransferMode TransferMode { get; set; }
        public bool KeepAliveEnabled { get; set; }
        public IAnonymousUriPrefixMatcher AnonymousUriPrefixMatcher { get; set; }
        public WebSocketTransportSettings WebSocketSettings { get; set; }
        public AuthenticationSchemes AuthenticationScheme { get; set; }
        public bool IsAuthenticationRequired => AuthenticationScheme.IsNotSet(AuthenticationSchemes.Anonymous);
        public bool IsAuthenticationSupported => AuthenticationScheme != AuthenticationSchemes.Anonymous;
    }
}
