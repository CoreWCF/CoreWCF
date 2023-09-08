// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.Extensions.Options;

namespace CoreWCF.Configuration
{
    internal class UnixDomainSocketOptionsSetup : IConfigureOptions<Channels.UnixDomainSocketOptions>
    {
        private readonly IServiceProvider _services;

        public UnixDomainSocketOptionsSetup(IServiceProvider services)
        {
            _services = services;
        }

        public void Configure(Channels.UnixDomainSocketOptions options)
        {
            options.ApplicationServices = _services;
        }
    }
}

