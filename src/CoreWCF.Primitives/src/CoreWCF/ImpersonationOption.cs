// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF
{
    public enum ImpersonationOption
    {
        NotAllowed,
        Allowed,
        Required,
    }

    static class ImpersonationOptionHelper
    {
        public static bool IsDefined(ImpersonationOption option)
        {
            return (option == ImpersonationOption.NotAllowed ||
                    option == ImpersonationOption.Allowed ||
                    option == ImpersonationOption.Required);
        }
    }
}
