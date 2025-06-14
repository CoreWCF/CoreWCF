// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Authentication;

namespace CoreWCF.Security.NegotiateInternal
{
    internal static class NTAuthenticationFacade
    {
        private static readonly int s_AssemblyMajorVersion = typeof(AuthenticationException).Assembly.GetName().Version.Major;

        internal static INTAuthenticationFacade Build() =>
            s_AssemblyMajorVersion switch
            {
                >= 8 => new NTAuthenticationNet8(),
                >= 7 => new NTAuthenticationNet7(),
                >= 5 => new NTAuthenticationNet5(),
                _ => new NTAuthenticationLegacy()
            };
    }
}
