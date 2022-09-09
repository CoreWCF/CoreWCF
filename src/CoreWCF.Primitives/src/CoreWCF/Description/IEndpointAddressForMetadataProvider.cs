// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.AspNetCore.Http;

namespace CoreWCF.Description
{
    public interface IMetadataEndpointAddressProvider
    {
        Uri GetEndpointAddress(HttpRequest httpRequest, Uri listenUri);
    }
}
