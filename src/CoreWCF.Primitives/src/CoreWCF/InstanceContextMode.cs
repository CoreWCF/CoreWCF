// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF
{
    public enum InstanceContextMode
    {
        PerSession,
        PerCall,
        Single,
    }

    internal static class InstanceContextModeHelper
    {
        public static bool IsDefined(InstanceContextMode x)
        {
            return
                x == InstanceContextMode.PerCall ||
                x == InstanceContextMode.PerSession ||
                x == InstanceContextMode.Single;
        }
    }
}