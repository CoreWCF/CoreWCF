using Microsoft.AspNetCore.Connections;
using CoreWCF.Channels.Framing;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace CoreWCF.Configuration
{
    public delegate Task HandshakeDelegate(FramingConnection connection);

    /// <summary>
    /// Defines a class that provides the mechanisms to configure a connection handshake pipeline.
    /// </summary>
    public interface IFramingConnectionHandshakeBuilder
    {
        /// <summary>
        /// Gets or sets the <see cref="IServiceProvider"/> that provides access to the application's service container.
        /// </summary>
        IServiceProvider HandshakeServices { get; set; }

        /// <summary>
        /// Gets a key/value collection that can be used to share data between middleware.
        /// </summary>
        IDictionary<string, object> Properties { get; }

        /// <summary>
        /// Adds a middleware delegate to the connection handshake pipeline.
        /// </summary>
        /// <param name="IConnectionHandshakeBuilder">The middleware delegate.</param>
        /// <returns>The <see cref="IFramingConnectionHandshakeBuilder"/>.</returns>
        IFramingConnectionHandshakeBuilder Use(Func<HandshakeDelegate, HandshakeDelegate> middleware);

        /// <summary>
        /// Creates a new <see cref="IFramingConnectionHandshakeBuilder"/> that shares the <see cref="Properties"/> of this
        /// <see cref="IFramingConnectionHandshakeBuilder"/>.
        /// </summary>
        /// <returns>The new <see cref="IFramingConnectionHandshakeBuilder"/>.</returns>
        IFramingConnectionHandshakeBuilder New();

        /// <summary>
        /// Builds the delegate used by this application to process ServiceModel Framed requests.
        /// </summary>
        /// <returns>The request handling delegate.</returns>
        HandshakeDelegate Build();
    }
}
