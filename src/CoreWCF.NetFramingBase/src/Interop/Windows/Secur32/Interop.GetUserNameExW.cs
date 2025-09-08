// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using System.Text;

internal static partial class Interop
{
    internal static partial class Secur32
    {
        internal static bool GetCurrentUpn(out string upn)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                upn = null;
                return false;
            }

            StringBuilder sb = new StringBuilder(1024);
            uint upnNameLength = (uint)sb.Capacity;

            if (GetUserNameEx(EXTENDED_NAME_FORMAT.NameUserPrincipal, sb, ref upnNameLength) == BOOLEAN.FALSE)
            {
                int error = Marshal.GetLastWin32Error();
                if (error == ERROR_MORE_DATA)
                {
                    sb.Capacity = (int)++upnNameLength;
                    if (GetUserNameEx(EXTENDED_NAME_FORMAT.NameUserPrincipal, sb, ref upnNameLength) == BOOLEAN.FALSE)
                    {
                        upn = null;
                        return false;
                    }
                }
                else
                {
                    upn = null;
                    return false;
                }
            }

            upn = sb.ToString();
            return true;
        }

        internal const int ERROR_MORE_DATA = 0xEA;

        [DllImport("secur32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern BOOLEAN GetUserNameEx(EXTENDED_NAME_FORMAT nameFormat, [Out] StringBuilder lpNameBuffer, ref uint domainNameLen);
    }
}
