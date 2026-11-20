// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;

namespace CoreWCF.Configuration
{
    public static class WebApplicationExtensions
    {
        /// <summary>
        /// Configures CoreWCF services on a <see cref="WebApplication"/>.
        /// </summary>
        /// <remarks>
        /// <see cref="WebApplication"/> implements both <see cref="IApplicationBuilder"/> and
        /// <see cref="IHost"/>, which makes the corresponding overloads on
        /// <see cref="ServiceModelApplicationBuilderExtensions"/> ambiguous. This overload
        /// resolves that ambiguity in favour of the <see cref="IApplicationBuilder"/> behavior.
        /// </remarks>
        public static IHost UseServiceModel(this WebApplication app, Action<IServiceBuilder> configureServices)
        {
            if (app == null)
            {
                throw new ArgumentNullException(nameof(app));
            }

            ((IApplicationBuilder)app).UseServiceModel(configureServices);
            return app;
        }
    }
}
