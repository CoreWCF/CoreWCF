// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net.WebSockets;

namespace CoreWCF.Channels
{
    /// <summary>
    /// IWebSocketCloseDetails is used to indicate why a web socket has closed.
    /// </summary>
    public interface IWebSocketCloseDetails
    {
        /// <summary>
        /// The reason why the remote endpoint initiated the close handshake.
        /// </summary>
        WebSocketCloseStatus? InputCloseStatus { get; }

        /// <summary>
        /// Optional description for why the close handshake has been initiated by the remote endpoint.
        /// </summary>
        string InputCloseStatusDescription { get; }

        /// <summary>
        /// Sets the output close status.
        /// </summary>
        /// <param name="closeStatus">The reason why the remote endpoint initiated the close handshake.</param>
        /// <param name="closeStatusDescription">Optional description for why the close handshake has been initiated by the remote endpoint.</param>
        void SetOutputCloseStatus(WebSocketCloseStatus closeStatus, string closeStatusDescription);
    }
}
