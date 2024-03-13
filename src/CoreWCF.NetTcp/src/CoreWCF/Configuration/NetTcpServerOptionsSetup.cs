// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Options;
using System;

namespace CoreWCF.Configuration
{
    internal class NetTcpServerOptionsSetup : IConfigureOptions<Channels.NetTcpOptions>
    {
        private readonly IServiceProvider _services;

        public NetTcpServerOptionsSetup(IServiceProvider services)
        {
            _services = services;
        }

        public void Configure(Channels.NetTcpOptions options)
        {
            options.ApplicationServices = _services;
        }
    }
}
