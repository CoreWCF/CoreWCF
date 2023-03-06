// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace Microsoft.AspNetCore.TestHost
{
    public static class TestHostExtensions
    {
        public static int GetHttpPort(this TestServer testServer)
        {
            foreach (var serverFeature in testServer.Features)
            {
                if (serverFeature.Key == typeof(IServerAddressesFeature))
                {
                    foreach (string address in ((IServerAddressesFeature)serverFeature.Value).Addresses)
                    {
                        if (address.StartsWith("http://"))
                        {
                            var index = address.LastIndexOf(':');
                            if (index == 4)
                            {
                                return 80;
                            }

                            return Int32.TryParse(address.Substring(index + 1, address.Length - (index + 1)), out int port)
                                ? port
                                : throw new NotSupportedException();
                        }
                    }
                }

            }

            throw new NotSupportedException();
        }
    }
}
