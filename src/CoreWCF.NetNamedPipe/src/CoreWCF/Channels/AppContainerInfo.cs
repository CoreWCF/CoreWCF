// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Text;
using CoreWCF.Runtime;
using CoreWCF.Security;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Win32.SafeHandles;
using static CoreWCF.Security.UnsafeNativeMethods;

namespace CoreWCF.Channels
{
    [SupportedOSPlatform("windows")]
    internal static class AppContainerInfo
    {
        private static readonly object s_lock = new object();
        private static volatile bool s_isRunningInAppContainerSet;
        private static bool s_isRunningInAppContainer;
        private static volatile SecurityIdentifier s_currentAppContainerSid;

        internal static bool IsRunningInAppContainer
        {
            get
            {
                if (!s_isRunningInAppContainerSet)
                {
                    lock (s_lock)
                    {
                        if (!s_isRunningInAppContainerSet)
                        {
                            s_isRunningInAppContainer = RunningInAppContainer();
                            s_isRunningInAppContainerSet = true;
                        }
                    }
                }

                return s_isRunningInAppContainer;
            }
        }

        internal static SecurityIdentifier GetCurrentAppContainerSid()
        {
            if (s_currentAppContainerSid == null)
            {
                lock (s_lock)
                {
                    if (s_currentAppContainerSid == null)
                    {
                        s_currentAppContainerSid = GetAppContainerSid();
                    }
                }
            }

            return s_currentAppContainerSid;
        }

        private static bool RunningInAppContainer()
        {
            uint tokenInfoLength = sizeof(int);
            byte[] tokenInformation = new byte[tokenInfoLength];
            if (!UnsafeNativeMethods.GetTokenInformation(GetCurrentProcessToken(),
                UnsafeNativeMethods.TOKEN_INFORMATION_CLASS.TokenIsAppContainer,
                tokenInformation, tokenInfoLength, out _))
            {
                int error = Marshal.GetLastWin32Error();
                throw new Win32Exception(error);
            }

            return BitConverter.ToInt32(tokenInformation, 0) != 0;
        }

        private static unsafe SecurityIdentifier GetAppContainerSid()
        {
            // First call to get the required buffer length
            uint returnLength = UnsafeNativeMethods.GetTokenInformationLength(
                    GetCurrentProcessToken(),
                    TOKEN_INFORMATION_CLASS.TokenAppContainerSid);

            byte[] tokenInformation = new byte[returnLength];
            fixed (byte* pTokenInformation = tokenInformation)
            {
                if (!UnsafeNativeMethods.GetTokenInformation(
                                                GetCurrentProcessToken(),
                                                TOKEN_INFORMATION_CLASS.TokenAppContainerSid,
                                                tokenInformation,
                                                returnLength,
                                                out returnLength))
                {
                    int errorCode = Marshal.GetLastWin32Error();
                    throw Fx.Exception.AsError(new Win32Exception(errorCode));
                }

                // TOKEN_APPCONTAINER_INFORMATION contains a single PSID field
                IntPtr sidPtr = Marshal.ReadIntPtr(tokenInformation, 0);
                return new SecurityIdentifier(sidPtr);
            }
        }

        // Shortcut for when you only need to a token to query information.
        // See https://learn.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-getcurrentprocesstoken for more information
        private static IntPtr GetCurrentProcessToken() => (IntPtr)(-4);
    }
}
