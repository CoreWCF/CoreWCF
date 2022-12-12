// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using CoreWCF.Channels;
using Microsoft.Extensions.Options;

namespace CoreWCF.Configuration
{
    internal class NetNamedPipeServerOptionsSetup : IConfigureOptions<Channels.NetNamedPipeOptions>
    {
        private readonly IServiceProvider _services;

        public NetNamedPipeServerOptionsSetup(IServiceProvider services)
        {
            _services = services;
        }

        public void Configure(Channels.NetNamedPipeOptions options)
        {
            options.ApplicationServices = _services;
        }
    }
}
