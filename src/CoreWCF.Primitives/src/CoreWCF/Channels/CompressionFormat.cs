// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Channels
{
    public enum CompressionFormat
    {
        /// <summary>
        /// Default to compression off
        /// </summary>
        None,

        /// <summary>
        /// GZip compression
        /// </summary>
        GZip,

        /// <summary>
        /// Deflate compression
        /// </summary>
        Deflate,
    }
}