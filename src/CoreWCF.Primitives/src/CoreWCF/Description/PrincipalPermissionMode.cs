// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace CoreWCF.Description
{
    public enum PrincipalPermissionMode
    {
        None,
        UseWindowsGroups,
        Custom,
        Always
    }

    internal static class PrincipalPermissionModeHelper
    {
        public static bool IsDefined(PrincipalPermissionMode principalPermissionMode)
        {
            return Enum.IsDefined(typeof(PrincipalPermissionMode), principalPermissionMode);
        }
    }
}
