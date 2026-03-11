// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Text;
using CoreWCF.Security;
using Microsoft.Win32.SafeHandles;

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
                        using SafeAccessTokenHandle token = GetCurrentProcessToken();
                        s_currentAppContainerSid = GetAppContainerSid(token);
                    }
                }
            }

            return s_currentAppContainerSid;
        }

        private static bool RunningInAppContainer()
        {
            using SafeAccessTokenHandle token = GetCurrentProcessToken();
            int tokenInfoLength = sizeof(int);
            byte[] tokenInformation = new byte[tokenInfoLength];
            if (!UnsafeNativeMethods.GetTokenInformation(token,
                UnsafeNativeMethods.TOKEN_INFORMATION_CLASS.TokenIsAppContainer,
                tokenInformation, tokenInfoLength, out _))
            {
                int error = Marshal.GetLastWin32Error();
                throw new Win32Exception(error);
            }

            return BitConverter.ToInt32(tokenInformation, 0) != 0;
        }

        private static SecurityIdentifier GetAppContainerSid(SafeAccessTokenHandle token)
        {
            // First call to get the required buffer length
            UnsafeNativeMethods.GetTokenInformation(token,
                UnsafeNativeMethods.TOKEN_INFORMATION_CLASS.TokenAppContainerSid,
                null, 0, out int lengthNeeded);

            if (lengthNeeded == 0)
            {
                int error = Marshal.GetLastWin32Error();
                throw new Win32Exception(error);
            }

            byte[] tokenInformation = new byte[lengthNeeded];
            if (!UnsafeNativeMethods.GetTokenInformation(token,
                UnsafeNativeMethods.TOKEN_INFORMATION_CLASS.TokenAppContainerSid,
                tokenInformation, lengthNeeded, out _))
            {
                int error = Marshal.GetLastWin32Error();
                throw new Win32Exception(error);
            }

            // TOKEN_APPCONTAINER_INFORMATION contains a single PSID field
            IntPtr sidPtr = Marshal.ReadIntPtr(tokenInformation, 0);
            return new SecurityIdentifier(sidPtr);
        }

        private static SafeAccessTokenHandle GetCurrentProcessToken()
        {
            if (!UnsafeNativeMethods.OpenProcessToken(
                UnsafeNativeMethods.GetCurrentProcess(),
                TokenAccessLevels.Query,
                out SafeCloseHandle tokenHandle))
            {
                int error = Marshal.GetLastWin32Error();
                throw new Win32Exception(error);
            }

            // Wrap in SafeAccessTokenHandle for compatibility with GetTokenInformation
            SafeAccessTokenHandle accessToken = new SafeAccessTokenHandle(tokenHandle.DangerousGetHandle());
            // Prevent the SafeCloseHandle from releasing the handle since SafeAccessTokenHandle now owns it
            tokenHandle.SetHandleAsInvalid();
            return accessToken;
        }
    }
}
