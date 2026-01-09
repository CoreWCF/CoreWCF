// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Microsoft.AspNetCore.Hosting
{
    public static class WebHostExtensions
    {
        public static int GetHttpPort(this IHost host) => host.GetHttpBaseAddressUri().Port;

        public static int GetHttpsPort(this IHost host) => host.GetHttpsBaseAddressUri().Port;

        public static Uri GetHttpBaseAddressUri(this IHost host)
        {
            var server = host.Services.GetRequiredService<IServer>();
            Uri uri = server.Features.Get<IServerAddressesFeature>().Addresses.Select(static address => new Uri(address)).FirstOrDefault(static uri => uri.Scheme == "http");
            return uri != null
                ? uri
                : throw new NotSupportedException();
        }

        public static Uri GetHttpsBaseAddressUri(this IHost host)
        {
            var server = host.Services.GetRequiredService<IServer>();
            Uri uri = server.Features.Get<IServerAddressesFeature>().Addresses.Select(static address => new Uri(address)).FirstOrDefault(static uri => uri.Scheme == "https");
            return uri != null
                ? uri
                : throw new NotSupportedException();
        }
    }
}
