// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Threading.Tasks;
using CoreWCF.Channels.Framing;

namespace CoreWCF.Configuration
{
    /// <summary>
    /// Represents a middleware that maps a request path to a sub-request pipeline.
    /// </summary>
    public class MapMiddleware
    {
        private readonly HandshakeDelegate _next;
        private readonly MapOptions _options;

        /// <summary>
        /// Creates a new instance of <see cref="MapMiddleware"/>.
        /// </summary>
        /// <param name="next">The delegate representing the next middleware in the request pipeline.</param>
        /// <param name="options">The middleware options.</param>
        public MapMiddleware(HandshakeDelegate next, MapOptions options)
        {
            _next = next ?? throw new ArgumentNullException(nameof(next));
            _options = options ?? throw new ArgumentNullException(nameof(options));
        }

        /// <summary>
        /// Executes the middleware.
        /// </summary>
        /// <param name="context">The <see cref="HttpContext"/> for the current request.</param>
        /// <returns>A task that represents the execution of this middleware.</returns>
        public async Task Invoke(FramingConnection connection)
        {
            if (connection == null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            if (_options.Predicate(connection))
            {
                await _options.Branch(connection);
            }
            else
            {
                await _next(connection);
            }
        }
    }
}
