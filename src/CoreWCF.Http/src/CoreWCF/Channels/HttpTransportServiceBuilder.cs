// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CoreWCF.Channels
{
    internal class HttpTransportServiceBuilder : ITransportServiceBuilder
    {
        private bool _configured = false;
        private readonly object _lock = new object();

        public void Configure(IApplicationBuilder app)
        {
            ILogger<HttpTransportServiceBuilder> logger = app.ApplicationServices.GetRequiredService<ILogger<HttpTransportServiceBuilder>>();
            if (!_configured)
            {
                lock (_lock)
                {
                    if (!_configured)
                    {
                        ConfigureCore(app, logger);
                        _configured = true;
                    }
                }
            }
        }

        private void ConfigureCore(IApplicationBuilder app, ILogger logger)
        {
            logger.LogDebug("Adding ServiceModelHttpMiddleware to app builder");
            app.UseMiddleware<ServiceModelHttpMiddleware>(app);
        }
    }
}
