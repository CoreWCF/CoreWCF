// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF
{
    public enum TransferMode
    {
        /// <summary>
        /// Buffer messages in both directions.
        /// </summary>
        Buffered = 0,

        /// <summary>
        /// Stream messages in both directions.
        /// </summary>
        Streamed = 1,

        /// <summary>
        /// Stream request messages, buffer response messages.
        /// </summary>
        StreamedRequest = 2,

        /// <summary>
        /// Buffer request messages, stream response messages.
        /// </summary>
        StreamedResponse = 3,
    }
}
