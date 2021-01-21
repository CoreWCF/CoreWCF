// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Configuration;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CoreWCF.Channels
{
    internal class HttpTransportServiceBuilder : ITransportServiceBuilder
    {
        private bool _configured = false;
        private DateTime _configuredTime = DateTime.MinValue;
        private readonly object _lock = new object();

        public void Configure(IApplicationBuilder app)
        {
            var logger = app.ApplicationServices.GetRequiredService<ILogger<HttpTransportServiceBuilder>>();
            logger.LogDebug($"Configure called _configured:{_configured} _configuredTime:{_configuredTime}");
            if (!_configured)
            {
                logger.LogDebug("!Configured");
                lock (_lock)
                {
                    if (!_configured)
                    {
                        logger.LogDebug("Still !Configured");
                        ConfigureCore(app);
                        _configured = true;
                        _configuredTime = DateTime.UtcNow;
                    }

                }
            }
        }

        private void ConfigureCore(IApplicationBuilder app)
        {
            var logger = app.ApplicationServices.GetRequiredService<ILogger<HttpTransportServiceBuilder>>();
            logger.LogDebug("Adding ServiceModelHttpMiddleware to app builder");
            app.UseMiddleware<ServiceModelHttpMiddleware>(app);
        }
    }
}