// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using CoreWCF.Runtime;
using Microsoft.Win32.SafeHandles;

namespace CoreWCF.Security
{
    internal static class UnsafeNativeMethods
    {
        public const string KERNEL32 = "kernel32.dll";
        public const string ADVAPI32 = "advapi32.dll";

        public const int ERROR_SUCCESS = 0;
        public const int ERROR_FILE_NOT_FOUND = 2;
        public const int ERROR_ACCESS_DENIED = 5;
        public const int ERROR_INVALID_HANDLE = 6;
        public const int ERROR_NOT_ENOUGH_MEMORY = 8;
        public const int ERROR_OUTOFMEMORY = 14;
        public const int ERROR_SHARING_VIOLATION = 32;
        public const int ERROR_HANDLE_EOF = 38;
        public const int ERROR_NETNAME_DELETED = 64;
        public const int ERROR_INVALID_PARAMETER = 87;
        public const int ERROR_BROKEN_PIPE = 109;
        public const int ERROR_ALREADY_EXISTS = 183;
        public const int ERROR_PIPE_BUSY = 231;
        public const int ERROR_NO_DATA = 232;
        public const int ERROR_PIPE_NOT_CONNECTED = 233;
        public const int ERROR_MORE_DATA = 234;
        public const int WAIT_TIMEOUT = 258;
        public const int ERROR_PIPE_CONNECTED = 535;
        public const int ERROR_OPERATION_ABORTED = 995;
        public const int ERROR_IO_PENDING = 997;
        public const int ERROR_SERVICE_ALREADY_RUNNING = 1056;
        public const int ERROR_SERVICE_DISABLED = 1058;
        public const int ERROR_NO_TRACKING_SERVICE = 1172;
        public const int ERROR_ALLOTTED_SPACE_EXCEEDED = 1344;
        public const int ERROR_NO_SYSTEM_RESOURCES = 1450;

        // When querying for the token length
        public const int ERROR_INSUFFICIENT_BUFFER = 122;

        public const int STATUS_PENDING = 0x103;

        // socket errors
        public const int WSAACCESS = 10013;
        public const int WSAEMFILE = 10024;
        public const int WSAEMSGSIZE = 10040;
        public const int WSAEADDRINUSE = 10048;
        public const int WSAEADDRNOTAVAIL = 10049;
        public const int WSAENETDOWN = 10050;
        public const int WSAENETUNREACH = 10051;
        public const int WSAENETRESET = 10052;
        public const int WSAECONNABORTED = 10053;
        public const int WSAECONNRESET = 10054;
        public const int WSAENOBUFS = 10055;
        public const int WSAESHUTDOWN = 10058;
        public const int WSAETIMEDOUT = 10060;
        public const int WSAECONNREFUSED = 10061;
        public const int WSAEHOSTDOWN = 10064;
        public const int WSAEHOSTUNREACH = 10065;

        public const int GENERIC_ALL = 0x10000000;
        public const int GENERIC_READ = unchecked((int)0x80000000);
        public const int GENERIC_WRITE = 0x40000000;
        public const int FILE_CREATE_PIPE_INSTANCE = 0x00000004;
        public const int FILE_WRITE_ATTRIBUTES = 0x00000100;
        public const int FILE_WRITE_DATA = 0x00000002;
        public const int FILE_WRITE_EA = 0x00000010;

        public const int FILE_FLAG_OVERLAPPED = 0x40000000;
        public const int FILE_FLAG_FIRST_PIPE_INSTANCE = 0x00080000;

        public const int PIPE_ACCESS_DUPLEX = 3;
        public const int PIPE_UNLIMITED_INSTANCES = 255;
        public const int PIPE_TYPE_BYTE = 0;
        public const int PIPE_TYPE_MESSAGE = 4;
        public const int PIPE_READMODE_BYTE = 0;
        public const int PIPE_READMODE_MESSAGE = 2;

        // VirtualAlloc constants
        public const int PAGE_READWRITE = 4;

        public const int FILE_MAP_WRITE = 2;
        public const int FILE_MAP_READ = 4;

        public const int FORMAT_MESSAGE_IGNORE_INSERTS = 0x00000200;
        public const int FORMAT_MESSAGE_FROM_SYSTEM = 0x00001000;
        public const int FORMAT_MESSAGE_ARGUMENT_ARRAY = 0x00002000;

        // ProcessToken constants
        public const uint STANDARD_RIGHTS_READ = 0x00020000;
        public const uint TOKEN_QUERY = 0x0008;

        // TODO: Try replacing lpSecurityDescriptor with byte[]. I think we can avoid pinning.
        [StructLayout(LayoutKind.Sequential)]
        internal class SECURITY_ATTRIBUTES
        {
            internal uint nLength = (uint)Marshal.SizeOf(typeof(SECURITY_ATTRIBUTES));
            internal IntPtr lpSecurityDescriptor = IntPtr.Zero;
            internal bool bInheritHandle = false;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        internal struct SID_AND_ATTRIBUTES
        {
            internal IntPtr Sid;
            internal SidAttribute Attributes;
        }

        [Flags]
        internal enum SidAttribute : uint
        {
            SE_GROUP_MANDATORY = 0x1, // The SID cannot have the SE_GROUP_ENABLED attribute cleared by a call to the AdjustTokenGroups function. However, you can use the CreateRestrictedToken function to convert a mandatory SID to a deny-only SID. 
            SE_GROUP_ENABLED_BY_DEFAULT = 0x2, // The SID is enabled by default. 
            SE_GROUP_ENABLED = 0x4, // The SID is enabled for access checks. When the system performs an access check, it checks for access-allowed and access-denied access control entries (ACEs) that apply to the SID. A SID without this attribute is ignored during an access check unless the SE_GROUP_USE_FOR_DENY_ONLY attribute is set.
            SE_GROUP_OWNER = 0x8, // The SID identifies a group account for which the user of the token is the owner of the group, or the SID can be assigned as the owner of the token or objects. 
            SE_GROUP_USE_FOR_DENY_ONLY = 0x10, // 
            SE_GROUP_RESOURCE = 0x20000000, // The SID identifies a domain-local group.Windows NT:  This value is not supported. 
            SE_GROUP_LOGON_ID = 0xC0000000, // The SID is a logon SID that identifies the logon session associated with an access token. 
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct TOKEN_GROUPS
        {
            internal int GroupCount;
            internal IntPtr Groups; // array of SID_AND_ATTRIBUTES
        }

        internal enum TOKEN_INFORMATION_CLASS : int
        {
            TokenUser = 1, // TOKEN_USER structure that contains the user account of the token. = 1, 
            TokenGroups, // a TOKEN_GROUPS structure that contains the group accounts associated with the token., 
            TokenPrivileges, // a TOKEN_PRIVILEGES structure that contains the privileges of the token., 
            TokenOwner, // a TOKEN_OWNER structure that contains the default owner security identifier (SID) for newly created objects., 
            TokenPrimaryGroup, // a TOKEN_PRIMARY_GROUP structure that contains the default primary group SID for newly created objects., 
            TokenDefaultDacl, // a TOKEN_DEFAULT_DACL structure that contains the default DACL for newly created objects., 
            TokenSource, // a TOKEN_SOURCE structure that contains the source of the token. TOKEN_QUERY_SOURCE access is needed to retrieve this information., 
            TokenType, // a TOKEN_TYPE value that indicates whether the token is a primary or impersonation token., 
            TokenImpersonationLevel, // a SECURITY_IMPERSONATION_LEVEL value that indicates the impersonation level of the token. If the access token is not an impersonation token, the function fails., 
            TokenStatistics, // a TOKEN_STATISTICS structure that contains various token statistics., 
            TokenRestrictedSids, // a TOKEN_GROUPS structure that contains the list of restricting SIDs in a restricted token., 
            TokenSessionId, // a DWORD value that indicates the Terminal Services session identifier that is associated with the token. If the token is associated with the Terminal Server console session, the session identifier is zero. If the token is associated with the Terminal Server client session, the session identifier is nonzero. In a non-Terminal Services environment, the session identifier is zero. If TokenSessionId is set with SetTokenInformation, the application must have the Act As Part Of the Operating System privilege, and the application must be enabled to set the session ID in a token.
            TokenGroupsAndPrivileges, // a TOKEN_GROUPS_AND_PRIVILEGES structure that contains the user SID, the group accounts, the restricted SIDs, and the authentication ID associated with the token., 
            TokenSessionReference, // Reserved,
            TokenSandBoxInert, // a DWORD value that is nonzero if the token includes the SANDBOX_INERT flag., 
            TokenAuditPolicy,
            TokenOrigin, // a TOKEN_ORIGIN value. If the token  resulted from a logon that used explicit credentials, such as passing a name, domain, and password to the  LogonUser function, then the TOKEN_ORIGIN structure will contain the ID of the logon session that created it. If the token resulted from  network authentication, such as a call to AcceptSecurityContext  or a call to LogonUser with dwLogonType set to LOGON32_LOGON_NETWORK or LOGON32_LOGON_NETWORK_CLEARTEXT, then this value will be zero.
            TokenElevationType,
            TokenLinkedToken,
            TokenElevation,
            TokenHasRestrictions,
            TokenAccessInformation,
            TokenVirtualizationAllowed,
            TokenVirtualizationEnabled,
            TokenIntegrityLevel,
            TokenUIAccess,
            TokenMandatoryPolicy,
            TokenLogonSid,
            TokenIsAppContainer,
            TokenCapabilities,
            TokenAppContainerSid,
            TokenAppContainerNumber,
            TokenUserClaimAttributes,
            TokenDeviceClaimAttributes,
            TokenRestrictedUserClaimAttributes,
            TokenRestrictedDeviceClaimAttributes,
            TokenDeviceGroups,
            TokenRestrictedDeviceGroups,
            MaxTokenInfoClass  // MaxTokenInfoClass should always be the last enum  
        }

        [DllImport(KERNEL32, SetLastError = true)]
        internal static unsafe extern bool CancelIoEx(SafePipeHandle handle, NativeOverlapped* lpOverlapped);

        [DllImport(KERNEL32)]
        internal static extern int CloseHandle(IntPtr handle);

        internal static void CloseInvalidOutSafeHandle(SafeCloseHandle handle)
        {
            // Workaround for 64-bit CLR bug - sometimes invalid SafeHandles come back null.
            if (handle != null)
            {
                Fx.Assert(handle.IsInvalid, "CloseInvalidOutSafeHandle called with a valid handle!");

                // Calls SuppressFinalize.
                handle.SetHandleAsInvalid();
            }
        }

        [DllImport(KERNEL32, CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern SafeFileMappingHandle CreateFileMapping(
            IntPtr fileHandle,
            SECURITY_ATTRIBUTES securityAttributes,
            int protect,
            int sizeHigh,
            int sizeLow,
            string name
        );

        [DllImport(KERNEL32, CharSet = CharSet.Unicode, SetLastError = true)]
        internal unsafe static extern SafePipeHandle CreateNamedPipe
        (
            string name,
            int openMode,
            int pipeMode,
            int maxInstances,
            int outBufSize,
            int inBufSize,
            int timeout,
            SECURITY_ATTRIBUTES securityAttributes
        );

        [DllImport(KERNEL32, CharSet = CharSet.Unicode)]
        internal static extern int FormatMessage
        (
            int dwFlags,
            IntPtr lpSource,
            int dwMessageId,
            int dwLanguageId,
            StringBuilder lpBuffer,
            int nSize,
            IntPtr arguments
        );

        [DllImport(ADVAPI32, ExactSpelling = true, SetLastError = true)]
        internal static extern bool GetTokenInformation(SafeAccessTokenHandle tokenHandle, TOKEN_INFORMATION_CLASS tokenInformationClass, [Out] byte[] pTokenInformation, int tokenInformationLength, out int returnLength);

        [DllImport(ADVAPI32, SetLastError = true, EntryPoint = "OpenProcessToken")]
        internal static extern bool
        OpenProcessToken(
            [In] IntPtr ProcessHandle,
            [In] TokenAccessLevels DesiredAccess,
            [Out] out SafeCloseHandle TokenHandle);

        [DllImport(KERNEL32, SetLastError = true)]
        internal static extern SafeViewOfFileHandle MapViewOfFile
        (
            SafeFileMappingHandle handle,
            int dwDesiredAccess,
            int dwFileOffsetHigh,
            int dwFileOffsetLow,
            IntPtr dwNumberOfBytesToMap
        );

        [DllImport(KERNEL32, ExactSpelling = true)]
        internal static extern int UnmapViewOfFile(IntPtr lpBaseAddress);

        [DllImport(KERNEL32, SetLastError = true)]
        internal static unsafe extern int WriteFile
        (
            SafePipeHandle handle,
            ref byte[] bytes,
            int numBytesToWrite,
            IntPtr numBytesWritten_mustBeZero,
            NativeOverlapped* lpOverlapped
        );
    }

    internal sealed class SafeFileMappingHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        internal SafeFileMappingHandle() : base(true) { }
        override protected bool ReleaseHandle() => UnsafeNativeMethods.CloseHandle(handle) != 0;
    }

    internal sealed class SafeViewOfFileHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        internal SafeViewOfFileHandle() : base(true) { }
        override protected bool ReleaseHandle()
        {
            if (UnsafeNativeMethods.UnmapViewOfFile(handle) != 0)
            {
                handle = IntPtr.Zero;
                return true;
            }

            return false;
        }
    }

    internal sealed class SafeCloseHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private SafeCloseHandle() : base(true) { }

        internal SafeCloseHandle(IntPtr handle, bool ownsHandle) : base(ownsHandle)
        {
            DiagnosticUtility.DebugAssert(handle == IntPtr.Zero || !ownsHandle, "Unsafe to create a SafeHandle that owns a pre-existing handle before the SafeHandle was created.");
            SetHandle(handle);
        }

        protected override bool ReleaseHandle()
        {
            return UnsafeNativeMethods.CloseHandle(handle) != 0;
        }
    }
}
