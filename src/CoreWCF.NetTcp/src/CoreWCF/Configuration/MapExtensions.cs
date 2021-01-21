// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Channels.Framing;

namespace CoreWCF.Configuration
{
    /// <summary>
    /// Extension methods for the <see cref="MapMiddleware"/>.
    /// </summary>
    public static class MapExtensions
    {
        /// <summary>
        /// Branches the handshake pipeline based on the result of a predicate. 
        /// If the predicate returns true the branch is executed.
        /// </summary>
        /// <param name="handshakeBuilder">The <see cref="IFramingConnectionHandshakeBuilder"/> instance.</param>
        /// <param name="predicate">The request path to match.</param>
        /// <param name="configuration">The branch to take for positive path matches.</param>
        /// <returns>The <see cref="IFramingConnectionHandshakeBuilder"/> instance.</returns>
        public static IFramingConnectionHandshakeBuilder Map(this IFramingConnectionHandshakeBuilder handshakeBuilder, Func<FramingConnection, bool> predicate, Action<IFramingConnectionHandshakeBuilder> configuration)
        {
            if (handshakeBuilder == null)
            {
                throw new ArgumentNullException(nameof(handshakeBuilder));
            }

            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            // create branch
            var branchBuilder = handshakeBuilder.New();
            configuration(branchBuilder);
            var branch = branchBuilder.Build();

            var options = new MapOptions
            {
                Branch = branch,
                Predicate = predicate,
            };
            return handshakeBuilder.Use(next => new MapMiddleware(next, options).Invoke);
        }
    }
}
