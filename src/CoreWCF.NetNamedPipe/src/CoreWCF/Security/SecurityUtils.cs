// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
using CoreWCF.Runtime;
using Microsoft.Win32.SafeHandles;

namespace CoreWCF.Security
{
    internal static class SecurityUtils
    {
        // The logon sid is generated on process start up so it is unique to this process.
        internal static SecurityIdentifier GetProcessLogonSid()
        {
            if (WindowsIdentity.GetCurrent().ImpersonationLevel == TokenImpersonationLevel.None)
            {
                // We're not impersonating so we can use WindowsIdentity.GetCurrent().AccessToken to
                // get the process logon SID.
                return GetProcessLogonSidCore();
            }

            // We're running impersonated which means WindowsIdentity.AccessToken won't provide us with
            // the process logon sid. Runing impersonated with a an invalid access token causes WindowIdentity
            // to call the Win32 api RevertToSelf before running the code.
            return WindowsIdentity.RunImpersonated(SafeAccessTokenHandle.InvalidHandle, () =>
            {
                // We're using the undocumented feature of passing an invalid handle to unimpersonate, so validate this is still true.
                Fx.Assert(WindowsIdentity.GetCurrent().ImpersonationLevel == TokenImpersonationLevel.None, "RunImpersonated with invalid handle didn't revert to process identity");
                return GetProcessLogonSidCore();
            });
        }

        private static SecurityIdentifier GetProcessLogonSidCore()
        {
            var processIdentity = WindowsIdentity.GetCurrent(TokenAccessLevels.Query);
            GCHandle pinnedArrayHandle = default;
            SafeAccessTokenHandle token = processIdentity.AccessToken;
            try
            {
                int lengthToken = GetTokenInformationLength(token, UnsafeNativeMethods.TOKEN_INFORMATION_CLASS.TokenGroups);
                byte[] tokenInformation = new byte[lengthToken];
                // tokenInformation needs to be pinned as it will be populated with structures that have pointers to other structures
                // within the same buffer. If GC moves it, those pointers will be invalid.
                pinnedArrayHandle = GCHandle.Alloc(tokenInformation, GCHandleType.Pinned);
                GetTokenInformation(token, UnsafeNativeMethods.TOKEN_INFORMATION_CLASS.TokenGroups, tokenInformation);
                Span<byte> tokenInfoSpan = tokenInformation;
                // Need to know the size of TOKEN_GROUPS to slice the Span so it will only end up with a single instance
                int tokenGroupSize = Marshal.SizeOf<UnsafeNativeMethods.TOKEN_GROUPS>();
                var tokenGroups = MemoryMarshal.Cast<byte, UnsafeNativeMethods.TOKEN_GROUPS>(tokenInfoSpan.Slice(0, tokenGroupSize));
                UnsafeNativeMethods.TOKEN_GROUPS tg = tokenGroups[0];
                // Need the offset of Groups as that's where the array of SID_AND_ATTRIBUTES starts. There is more data in the buffer after the array so
                // slice the Span to start at the start of the array, and end after TOKEN_GROUPS.GroupCount number of SID_AND_ATTRIBUTES items.
                int offsetOfSids = Marshal.OffsetOf<UnsafeNativeMethods.TOKEN_GROUPS>("Groups").ToInt32(); // Offset of Groups field
                var sidsSpan = MemoryMarshal.Cast<byte, UnsafeNativeMethods.SID_AND_ATTRIBUTES>(tokenInfoSpan.Slice(offsetOfSids, tg.GroupCount * Marshal.SizeOf<UnsafeNativeMethods.SID_AND_ATTRIBUTES>()));
                Fx.Assert(sidsSpan.Length == tg.GroupCount, "sidsSpan slice is the wrong size");
                for (int i = 0; i < tg.GroupCount; i++)
                {
                    if ((sidsSpan[i].Attributes & UnsafeNativeMethods.SidAttribute.SE_GROUP_LOGON_ID) == UnsafeNativeMethods.SidAttribute.SE_GROUP_LOGON_ID)
                    {
                        return new SecurityIdentifier(sidsSpan[i].Sid);
                    }
                }
                return new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
            }
            finally
            {
                pinnedArrayHandle.Free();
                processIdentity.Dispose();
            }
        }

        private static void GetTokenInformation(SafeAccessTokenHandle token, UnsafeNativeMethods.TOKEN_INFORMATION_CLASS tic, byte[] tokenInformation)
        {
            if (!UnsafeNativeMethods.GetTokenInformation(token, tic, tokenInformation, tokenInformation.Length, out _))
            {
                int error = Marshal.GetLastWin32Error();
                throw new Win32Exception(error);
            }
        }

        private static int GetTokenInformationLength(SafeAccessTokenHandle token, UnsafeNativeMethods.TOKEN_INFORMATION_CLASS tic)
        {
            int lengthNeeded;
            bool success = UnsafeNativeMethods.GetTokenInformation(token, tic, null, 0, out lengthNeeded);
            if (!success)
            {
                int error = Marshal.GetLastWin32Error();
                if (error != UnsafeNativeMethods.ERROR_INSUFFICIENT_BUFFER)
                {
                    throw new Win32Exception(error);
                }
            }

            return lengthNeeded;
        }
    }
}
