// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Channels;

internal class AuthorizationCapabilities : IAuthorizationCapabilities
{
    public bool SupportsAuthorizationData { get; }

    public AuthorizationCapabilities(bool supportsAuthorizationData)
    {
        SupportsAuthorizationData = supportsAuthorizationData;
    }
}
