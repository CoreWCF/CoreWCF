﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF
{
    public enum ReleaseInstanceMode
    {
        None = 0,
        BeforeCall = 1,
        AfterCall = 2,
        BeforeAndAfterCall = 3,
    }

    internal static class ReleaseInstanceModeHelper
    {
        public static bool IsDefined(ReleaseInstanceMode x)
        {
            return
                x == ReleaseInstanceMode.None ||
                x == ReleaseInstanceMode.BeforeCall ||
                x == ReleaseInstanceMode.AfterCall ||
                x == ReleaseInstanceMode.BeforeAndAfterCall ||
                false;
        }
    }
}