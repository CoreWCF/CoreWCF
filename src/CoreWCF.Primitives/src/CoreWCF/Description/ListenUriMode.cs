// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Description
{
    public enum ListenUriMode
    {
        Explicit,
        Unique,
    }

    internal static class ListenUriModeHelper
    {
        public static bool IsDefined(ListenUriMode mode)
        {
            return mode == ListenUriMode.Explicit
                || mode == ListenUriMode.Unique;
        }
    }
}