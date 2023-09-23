// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.Configuration
{
    public static class WebApplicationExtensions
    {
        public static Microsoft.Extensions.Hosting.IHost UseServiceModel(this Microsoft.AspNetCore.Builder.WebApplication app, Action<IServiceBuilder> configureServices)
        {
            if (app == null)
            {
                throw new ArgumentNullException(nameof(app));
            }

            ((Microsoft.AspNetCore.Builder.IApplicationBuilder)app).UseServiceModel(configureServices);
            return app;
        }
    }
}
