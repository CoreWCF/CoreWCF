using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;

namespace Helpers
{
    public static class TcpPortHelper
    {
        public static int GetFreeTcpPort()
        {
            TcpListener listener = new (IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}

namespace Microsoft.AspNetCore.Hosting
{
    public static class WebHostExtensions
    {

        public static int GetHttpPort(this IWebHost webHost)
        {
            foreach (var serverFeature in webHost.ServerFeatures)
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

        public static int GetHttpsPort(this IWebHost webHost)
        {
            foreach (var serverFeature in webHost.ServerFeatures)
            {
                if (serverFeature.Key == typeof(IServerAddressesFeature))
                {
                    foreach (string address in ((IServerAddressesFeature)serverFeature.Value).Addresses)
                    {
                        if (address.StartsWith("https://"))
                        {
                            var index = address.LastIndexOf(':');
                            if (index == 5)
                            {
                                return 443;
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

        public static Uri GetHttpBaseAddressUri(this IWebHost webHost)
        {
            foreach (var serverFeature in webHost.ServerFeatures)
            {
                if (serverFeature.Key == typeof(IServerAddressesFeature))
                {
                    foreach (string address in ((IServerAddressesFeature)serverFeature.Value).Addresses)
                    {
                        if (address.StartsWith("http://"))
                        {
                            return Uri.TryCreate(address.Replace("127.0.0.1", "localhost"), UriKind.RelativeOrAbsolute, out var uri)
                                ? uri
                                : throw new NotSupportedException();
                        }
                    }
                }

            }

            throw new NotSupportedException();
        }

        public static Uri GetHttpsBaseAddressUri(this IWebHost webHost)
        {
            foreach (var serverFeature in webHost.ServerFeatures)
            {
                if (serverFeature.Key == typeof(IServerAddressesFeature))
                {
                    foreach (string address in ((IServerAddressesFeature)serverFeature.Value).Addresses)
                    {
                        if (address.StartsWith("https://"))
                        {
                            return Uri.TryCreate(address, UriKind.RelativeOrAbsolute, out var uri)
                                ? uri
                                : throw new NotSupportedException();
                        }
                    }
                }

            }

            throw new NotSupportedException();
        }
    }
}
