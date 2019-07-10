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

    static class PrincipalPermissionModeHelper
    {
        public static bool IsDefined(PrincipalPermissionMode principalPermissionMode)
        {
            return Enum.IsDefined(typeof(PrincipalPermissionMode), principalPermissionMode);
        }
    }
}
