// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.Channels
{
    internal class NetFramingTransportSettings : ITransportFactorySettings
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
        public IAnonymousUriPrefixMatcher AnonymousUriPrefixMatcher { get; set; }
    }
}
