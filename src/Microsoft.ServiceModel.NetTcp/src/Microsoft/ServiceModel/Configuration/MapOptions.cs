using Microsoft.AspNetCore.Connections;
using Microsoft.ServiceModel.Channels.Framing;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.ServiceModel.Configuration
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
