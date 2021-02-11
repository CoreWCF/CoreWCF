// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.WebSockets;

namespace CoreWCF.Channels
{
    public interface IWebSocketCloseDetails
    {
        WebSocketCloseStatus? InputCloseStatus { get; }
        string InputCloseStatusDescription { get; }
        void SetOutputCloseStatus(WebSocketCloseStatus closeStatus, string closeStatusDescription);
    }
}
