// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF
{
    public enum ConcurrencyMode
    {
        Single, // This is first so it is ConcurrencyMode.default
        Reentrant,
        Multiple
    }

    internal static class ConcurrencyModeHelper
    {
        public static bool IsDefined(ConcurrencyMode x)
        {
            return
                x == ConcurrencyMode.Single ||
                x == ConcurrencyMode.Reentrant ||
                x == ConcurrencyMode.Multiple;
        }
    }
}