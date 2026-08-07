// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Security.NegotiateInternal
{
    internal static class NTAuthenticationFacade
    {
        internal static INTAuthenticationFacade Build() => new NTAuthenticationNet8();
    }
}
