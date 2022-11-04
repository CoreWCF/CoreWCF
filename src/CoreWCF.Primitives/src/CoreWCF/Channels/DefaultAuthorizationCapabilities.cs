// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Channels;

public class DefaultAuthorizationCapabilities : IAuthorizationCapabilities
{
    public bool SupportsAuthorizationData { get; }

    public DefaultAuthorizationCapabilities(bool supportsAuthorizationData)
    {
        SupportsAuthorizationData = supportsAuthorizationData;
    }
}
