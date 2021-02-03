// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Security.Tokens
{
    internal interface ISecurityContextSecurityTokenCacheProvider
    {
        ISecurityContextSecurityTokenCache TokenCache { get; }
    }
}
