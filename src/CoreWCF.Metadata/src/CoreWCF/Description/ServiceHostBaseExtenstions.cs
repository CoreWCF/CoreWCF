// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.Description
{
    internal static class ServiceHostBaseExtenstions
    {
        internal static Uri GetVia(this ServiceHostBase serviceHost, string scheme, Uri address)
        {
            if (!address.IsAbsoluteUri)
            {
                Uri baseAddress = null;
                foreach (var ba in serviceHost.BaseAddresses)
                {
                    if(ba.Scheme.Equals(scheme))
                    {
                        baseAddress = ba;
                        break;
                    }
                }

                if (baseAddress == null)
                {
                    return null;
                }

                return GetUri(baseAddress, address.OriginalString);
            }

            return address;
        }

        private static Uri GetUri(Uri baseUri, string path)
        {
            if (path.StartsWith("/", StringComparison.Ordinal) || path.StartsWith("\\", StringComparison.Ordinal))
            {
                int i = 1;
                for (; i < path.Length; ++i)
                {
                    if (path[i] != '/' && path[i] != '\\')
                    {
                        break;
                    }
                }
                path = path.Substring(i);
            }

            if (path.Length == 0)
                return baseUri;

            if (!baseUri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal))
            {
                baseUri = new Uri(baseUri.AbsoluteUri + "/");
            }
            return new Uri(baseUri, path);
        }
    }
}
