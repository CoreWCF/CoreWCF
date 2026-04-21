// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.Security.NegotiateInternal
{
    internal static class NTAuthenticationFacade
    {
        private static readonly int s_runtimeMajorVersion = Environment.Version.Major;

        internal static INTAuthenticationFacade Build() =>

            s_runtimeMajorVersion switch
            {
                >= 8 => new NTAuthenticationNet8(),
                _ => throw new PlatformNotSupportedException($"Unsupported runtime version: {Environment.Version}")
            };
    }
}
