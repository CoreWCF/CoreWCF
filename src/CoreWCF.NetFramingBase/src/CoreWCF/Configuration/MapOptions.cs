// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Channels.Framing;

namespace CoreWCF.Configuration
{
    /// <summary>
    /// Options for the <see cref="MapMiddleware"/>.
    /// </summary>
    public class MapOptions
    {
        /// <summary>
        /// The path to match.
        /// </summary>
        public Func<FramingConnection, bool> Predicate { get; set; }

        /// <summary>
        /// The branch taken for a positive match.
        /// </summary>
        public HandshakeDelegate Branch { get; set; }
    }
}
