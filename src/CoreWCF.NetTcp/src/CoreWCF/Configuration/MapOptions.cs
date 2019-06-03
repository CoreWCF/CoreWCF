using Microsoft.AspNetCore.Connections;
using CoreWCF.Channels.Framing;
using System;
using System.Collections.Generic;
using System.Text;

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
