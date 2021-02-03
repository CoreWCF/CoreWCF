// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;

namespace CoreWCF.Channels
{
    internal interface IHttpTransportFactorySettings : ITransportFactorySettings
    {
        int MaxBufferSize { get; }
        TransferMode TransferMode { get; }
        bool KeepAliveEnabled { get; set; }
        IAnonymousUriPrefixMatcher AnonymousUriPrefixMatcher { get; }
        WebSocketTransportSettings WebSocketSettings { get; }
        AuthenticationSchemes AuthenticationScheme { get; }
        bool IsAuthenticationRequired { get; }
        bool IsAuthenticationSupported { get; }
    }
}
