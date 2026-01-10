// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Linq;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace Microsoft.AspNetCore.Hosting
{
    public static class WebHostExtensions
    {
        public static int GetHttpPort(this IWebHost webHost) => webHost.GetHttpBaseAddressUri().Port;

        public static int GetHttpsPort(this IWebHost webHost) => webHost.GetHttpsBaseAddressUri().Port;

        public static Uri GetHttpBaseAddressUri(this IWebHost webHost)
        {
            Uri uri = webHost.ServerFeatures.Get<IServerAddressesFeature>().Addresses.Select(static address => new Uri(address)).FirstOrDefault(static uri => uri.Scheme == "http");
            return uri != null
                ? uri
                : throw new NotSupportedException();
        }

        public static Uri GetHttpsBaseAddressUri(this IWebHost webHost)
        {
            Uri uri = webHost.ServerFeatures.Get<IServerAddressesFeature>().Addresses.Select(static address => new Uri(address)).FirstOrDefault(static uri => uri.Scheme == "https");
            return uri != null
                ? uri
                : throw new NotSupportedException();
        }
    }
}
