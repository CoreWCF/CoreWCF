// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using CoreWCF.Configuration;
using Microsoft.AspNetCore.Builder;

namespace CoreWCF.Channels
{
    internal class MetadataTransportServiceBuilder : ITransportServiceBuilder
    {
        public void Configure(IApplicationBuilder app)
        {
            app.UseMiddleware<MetadataMiddleware>(app);
        }
    }
}
