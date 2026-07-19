// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace CoreWCF.UnixDomainSocket.Security
{
    internal static class UnixDomainSocketInterop
    {

        internal static bool TryGetCredentials(this Socket socket, out uint processId, out uint userId, out uint groupId)
        {
            try
            {
                if (OperatingSystem.IsWindows())
                    throw new PlatformNotSupportedException();

                Span<uint> ucred = stackalloc uint[3];
                int bytesWritten;
                if (OperatingSystem.IsMacOS())
                {
                    bytesWritten = socket.GetRawSocketOption(0, 2, MemoryMarshal.AsBytes(ucred)); // get processid
                    processId = ucred[0];
                    userId = 0;
                    groupId = 0;
                    return true;
                }
                else
                {
                    const int SOL_SOCKET = 1;
                    const int SO_PEERCRED = 17;
                    bytesWritten = socket.GetRawSocketOption(SOL_SOCKET, SO_PEERCRED, MemoryMarshal.AsBytes(ucred));
                }
                processId = ucred[0];
                userId = ucred[1];
                groupId = ucred[2];
                return bytesWritten == (ucred.Length * sizeof(uint));
            }
            catch(Exception ex)
            {
                throw;
            }
        }

    }

    internal static class NativeSysCall
    {
        private const string LIBC = "libc";
        private const int InitialBufferSize = 1024;
        // Hard upper bound to avoid unbounded growth on a misbehaving NSS implementation.
        private const int MaxBufferSize = 16 * 1024 * 1024;
        // POSIX errno value for "buffer too small". Both Linux and macOS use 34.
        private const int ERANGE = 34;

        [DllImport(LIBC, SetLastError = false)]
        private static extern unsafe int getpwuid_r(uint uid, PasswdRaw* pwd, byte* buf, nuint buflen, PasswdRaw** result);

        [DllImport(LIBC, SetLastError = false)]
        private static extern unsafe int getgrgid_r(uint gid, GroupRaw* grp, byte* buf, nuint buflen, GroupRaw** result);

        [StructLayout(LayoutKind.Sequential)]
        private unsafe struct PasswdRaw
        {
            public byte* pw_name;
            public byte* pw_passwd;
            public uint  pw_uid;
            public uint  pw_gid;
            public byte* pw_gecos;
            public byte* pw_dir;
            public byte* pw_shell;
        }

        [StructLayout(LayoutKind.Sequential)]
        private unsafe struct GroupRaw
        {
            public byte*  gr_name;
            public byte*  gr_passwd;
            public uint   gr_gid;
            // gr_mem is char**: a NULL-terminated array of char*.
            public byte** gr_mem;
        }

        internal static UserInfo GetUserInfo(uint uid)
        {
            int bufSize = InitialBufferSize;
            while (true)
            {
                byte[] buf = ArrayPool<byte>.Shared.Rent(bufSize);
                try
                {
                    int err = ReadUserInfo(uid, buf, out UserInfo info, out bool found);
                    if (err == 0)
                    {
                        return found ? info : null;
                    }
                    if (err == ERANGE && bufSize < MaxBufferSize)
                    {
                        bufSize = Math.Min(bufSize * 2, MaxBufferSize);
                        continue;
                    }
                    return null;
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buf);
                }
            }
        }

        internal static GroupInfo GetGroupInfo(uint gid)
        {
            int bufSize = InitialBufferSize;
            while (true)
            {
                byte[] buf = ArrayPool<byte>.Shared.Rent(bufSize);
                try
                {
                    int err = ReadGroupInfo(gid, buf, out GroupInfo info, out bool found);
                    if (err == 0)
                    {
                        return found ? info : null;
                    }
                    if (err == ERANGE && bufSize < MaxBufferSize)
                    {
                        bufSize = Math.Min(bufSize * 2, MaxBufferSize);
                        continue;
                    }
                    return null;
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buf);
                }
            }
        }

        private static unsafe int ReadUserInfo(uint uid, byte[] buf, out UserInfo info, out bool found)
        {
            info = null;
            found = false;
            PasswdRaw pwd;
            PasswdRaw* result;
            int err;
            fixed (byte* pBuf = buf)
            {
                err = getpwuid_r(uid, &pwd, pBuf, (nuint)buf.Length, &result);
                if (err != 0 || result == null)
                {
                    return err;
                }
                string name = Marshal.PtrToStringAnsi((IntPtr)pwd.pw_name);
                if (name == null)
                {
                    return 0;
                }
                string directory = Marshal.PtrToStringAnsi((IntPtr)pwd.pw_dir);
                string shell = Marshal.PtrToStringAnsi((IntPtr)pwd.pw_shell);
                info = new UserInfo(name, pwd.pw_uid, pwd.pw_gid, directory, shell);
                found = true;
                return 0;
            }
        }

        private static unsafe int ReadGroupInfo(uint gid, byte[] buf, out GroupInfo info, out bool found)
        {
            info = null;
            found = false;
            GroupRaw grp;
            GroupRaw* result;
            int err;
            fixed (byte* pBuf = buf)
            {
                err = getgrgid_r(gid, &grp, pBuf, (nuint)buf.Length, &result);
                if (err != 0 || result == null)
                {
                    return err;
                }
                string name = Marshal.PtrToStringAnsi((IntPtr)grp.gr_name);
                if (name == null)
                {
                    return 0;
                }
                string[] members = MaterializeMembers(grp.gr_mem);
                info = new GroupInfo(name, grp.gr_gid, members);
                found = true;
                return 0;
            }
        }

        private static unsafe string[] MaterializeMembers(byte** gr_mem)
        {
            if (gr_mem == null)
            {
                return Array.Empty<string>();
            }
            var list = new List<string>();
            for (int i = 0; gr_mem[i] != null; i++)
            {
                string member = Marshal.PtrToStringAnsi((IntPtr)gr_mem[i]);
                if (member != null)
                {
                    list.Add(member);
                }
            }
            return list.ToArray();
        }
    }

    internal sealed class GroupInfo
    {
        public string Name { get; }
        public uint Id { get; }
        public string[] Members { get; }

        internal GroupInfo(string name, uint id, string[] members)
        {
            Name = name;
            Id = id;
            Members = members ?? Array.Empty<string>();
        }
    }

    internal class UserInfo
    {
        public string Name { get; }
        public uint Uid { get; }
        public uint Gid { get; }
        public string Directory { get; }
        public string Shell { get; }

        internal UserInfo(string name, uint uid, uint gid, string directory, string shell)
        {
            Name = name;
            Uid = uid;
            Gid = gid;
            Directory = directory;
            Shell = shell;
        }
    }
}
