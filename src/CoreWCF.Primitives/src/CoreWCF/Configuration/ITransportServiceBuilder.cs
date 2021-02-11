// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Builder;

namespace CoreWCF.Configuration
{
    public interface ITransportServiceBuilder
    {
        void Configure(IApplicationBuilder app);
    }
}
