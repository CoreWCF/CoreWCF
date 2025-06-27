// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.ComponentModel;
using System.Security.Principal;
using System.Security.AccessControl;
using CoreWCF.IO;
using System.Globalization;
using System.Threading;
using CoreWCF.Security;
using Microsoft.Extensions.Logging;
using System.Runtime.Versioning;

namespace CoreWCF.Channels
{
    // Handles manipulating the net.pipe uri to a predictable shared memory path. If the path is too long,
    // it's shortened by using a hash algorithm. This matches the algorithm the client uses to find
    // the shared memory path. The shared memory will then contain the actual named pipe name which is
    // unique from one run to the next.
    [SupportedOSPlatform("windows")]
    internal static class PipeUri
    {
        public static void Validate(Uri uri)
        {
            if (uri.Scheme != Uri.UriSchemeNetPipe)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(nameof(uri), SR.PipeUriSchemeWrong);
        }

        public static string BuildSharedMemoryName(Uri uri, HostNameComparisonMode hostNameComparisonMode, bool global)
        {
            string path = GetPath(uri);
            string host = null;

            switch (hostNameComparisonMode)
            {
                case HostNameComparisonMode.StrongWildcard:
                    host = "+";
                    break;
                case HostNameComparisonMode.Exact:
                    host = uri.Host;
                    break;
                case HostNameComparisonMode.WeakWildcard:
                    host = "*";
                    break;
            }

            return BuildSharedMemoryName(host, path, global);
        }

        private static string BuildSharedMemoryName(string hostName, string path, bool global)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(Uri.UriSchemeNetPipe);
            builder.Append("://");
            builder.Append(hostName.ToUpperInvariant());
            builder.Append(path);
            string canonicalName = builder.ToString();

            byte[] canonicalBytes = Encoding.UTF8.GetBytes(canonicalName);
            byte[] hashedBytes;
            string separator;

            if (canonicalBytes.Length >= 128)
            {
                using (HashAlgorithm hash = GetHashAlgorithm())
                {
                    hashedBytes = hash.ComputeHash(canonicalBytes);
                }
                separator = ":H";
            }
            else
            {
                hashedBytes = canonicalBytes;
                separator = ":E";
            }

            builder = new StringBuilder();
            if (global)
            {
                // we may need to create the shared memory in the global namespace so we work with terminal services+admin 
                builder.Append(@"Global\");
            }
            else
            {
                builder.Append(@"Local\");
            }
            builder.Append(Uri.UriSchemeNetPipe);
            builder.Append(separator);
            builder.Append(Convert.ToBase64String(hashedBytes));
            return builder.ToString();
        }

        public static string GetPath(Uri uri)
        {
            string path = uri.LocalPath.ToUpperInvariant();
            if (!path.EndsWith("/", StringComparison.Ordinal))
                path = path + "/";
            return path;
        }

        internal const string UseSha1InPipeConnectionGetHashAlgorithmString = "Switch.System.ServiceModel.UseSha1InPipeConnectionGetHashAlgorithm";
        internal static bool s_useSha1InPipeConnectionGetHashAlgorithm = AppContext.TryGetSwitch(UseSha1InPipeConnectionGetHashAlgorithmString, out bool enabled) && enabled;

        private static HashAlgorithm GetHashAlgorithm()
        {
            if (s_useSha1InPipeConnectionGetHashAlgorithm)
            {
                return SHA1.Create();
            }
            else
            {
                return SHA256.Create();
            }
        }
    }

    // This class handles creating a shared memory object which holds the guid of the actual
    // named pipe that clients will connect to.
    [SupportedOSPlatform("windows")]
    internal unsafe class PipeSharedMemory : IDisposable
    {
        internal const string PipePrefix = @"\\.\pipe\";
        private SafeFileMappingHandle _fileMapping;
        private string _pipeName;
        private string _pipeNameGuidPart;
        private readonly Uri _pipeUri;

        private PipeSharedMemory(SafeFileMappingHandle fileMapping, Uri pipeUri)
            : this(fileMapping, pipeUri, null)
        {
        }

        private PipeSharedMemory(SafeFileMappingHandle fileMapping, Uri pipeUri, string pipeName)
        {
            _pipeName = pipeName;
            _fileMapping = fileMapping;
            _pipeUri = pipeUri;
        }

        public static PipeSharedMemory Create(List<SecurityIdentifier> allowedSids, Uri pipeUri, string sharedMemoryName, ILogger logger)
        {
            PipeSharedMemory result;
            if (TryCreate(allowedSids, pipeUri, sharedMemoryName, logger, out result))
            {
                return result;
            }
            else
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreatePipeNameInUseException(UnsafeNativeMethods.ERROR_ACCESS_DENIED, pipeUri));
            }
        }

        public static bool TryCreate(List<SecurityIdentifier> allowedSids, Uri pipeUri, string sharedMemoryName, ILogger logger, out PipeSharedMemory result)
        {
            Guid pipeGuid = Guid.NewGuid();
            string pipeName = BuildPipeName(pipeGuid.ToString());
            byte[] binarySecurityDescriptor;
            try
            {
                binarySecurityDescriptor = SecurityDescriptorHelper.FromSecurityIdentifiers(allowedSids, UnsafeNativeMethods.GENERIC_READ, logger);
            }
            catch (Win32Exception e)
            {
                // While Win32exceptions are not expected, if they do occur we need to obey the pipe/communication exception model.
                Exception innerException = new PipeException(e.Message, e);
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CommunicationException(innerException.Message, innerException));
            }

            SafeFileMappingHandle fileMapping;
            int error;
            result = null;
            fixed (byte* pinnedSecurityDescriptor = binarySecurityDescriptor)
            {
                UnsafeNativeMethods.SECURITY_ATTRIBUTES securityAttributes = new UnsafeNativeMethods.SECURITY_ATTRIBUTES();
                securityAttributes.lpSecurityDescriptor = (IntPtr)pinnedSecurityDescriptor;

                fileMapping = UnsafeNativeMethods.CreateFileMapping((IntPtr)(-1), securityAttributes,
                    UnsafeNativeMethods.PAGE_READWRITE, 0, sizeof(SharedMemoryContents), sharedMemoryName);
                error = Marshal.GetLastWin32Error();
            }

            if (fileMapping.IsInvalid)
            {
                fileMapping.SetHandleAsInvalid();
                if (error == UnsafeNativeMethods.ERROR_ACCESS_DENIED)
                {
                    return false;
                }
                else
                {
                    Exception innerException = new PipeException(SR.Format(SR.PipeNameCantBeReserved,
                        pipeUri.AbsoluteUri, PipeError.GetErrorString(error)), error);
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new AddressAccessDeniedException(innerException.Message, innerException));
                }
            }

            // now we have a valid file mapping handle
            if (error == UnsafeNativeMethods.ERROR_ALREADY_EXISTS)
            {
                fileMapping.Close();
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreatePipeNameInUseException(error, pipeUri));
            }

            PipeSharedMemory pipeSharedMemory = new PipeSharedMemory(fileMapping, pipeUri, pipeName);
            bool disposeSharedMemory = true;
            try
            {
                pipeSharedMemory.InitializeContents(pipeGuid);
                disposeSharedMemory = false;
                result = pipeSharedMemory;

                //if (TD.PipeSharedMemoryCreatedIsEnabled())
                //{
                //    TD.PipeSharedMemoryCreated(sharedMemoryName);
                //}
                return true;
            }
            finally
            {
                if (disposeSharedMemory)
                {
                    pipeSharedMemory.Dispose();
                }
            }
        }

        private void InitializeContents(Guid pipeGuid)
        {
            SafeViewOfFileHandle view = GetView(true);
            try
            {
                SharedMemoryContents* contents = (SharedMemoryContents*)view.DangerousGetHandle();
                contents->pipeGuid = pipeGuid;
                Thread.MemoryBarrier();
                contents->isInitialized = true;
            }
            finally
            {
                view.Close();
            }
        }

        public static Exception CreatePipeNameInUseException(int error, Uri pipeUri)
        {
            Exception innerException = new PipeException(SR.Format(SR.PipeNameInUse, pipeUri.AbsoluteUri), error);
            return new AddressAlreadyInUseException(innerException.Message, innerException);
        }

        private SafeViewOfFileHandle GetView(bool writable)
        {
            SafeViewOfFileHandle handle = UnsafeNativeMethods.MapViewOfFile(_fileMapping,
                writable ? UnsafeNativeMethods.FILE_MAP_WRITE : UnsafeNativeMethods.FILE_MAP_READ,
                0, 0, (IntPtr)sizeof(SharedMemoryContents));
            if (handle.IsInvalid)
            {
                int error = Marshal.GetLastWin32Error();
                handle.SetHandleAsInvalid();
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreatePipeNameCannotBeAccessedException(error, _pipeUri));
            }
            return handle;
        }

        public void Dispose()
        {
            if (_fileMapping != null)
            {
                _fileMapping.Dispose();
                _fileMapping = null;
            }
        }

        public string PipeName
        {
            get
            {
                if (_pipeName == null)
                {
                    SafeViewOfFileHandle view = GetView(false);
                    try
                    {
                        SharedMemoryContents* contents = (SharedMemoryContents*)view.DangerousGetHandle();
                        if (contents->isInitialized)
                        {
                            Thread.MemoryBarrier();
                            _pipeNameGuidPart = contents->pipeGuid.ToString();
                            _pipeName = BuildPipeName(_pipeNameGuidPart);
                        }
                    }
                    finally
                    {
                        view.Close();
                    }
                }

                return _pipeName;
            }
        }

        private static Exception CreatePipeNameCannotBeAccessedException(int error, Uri pipeUri)
        {
            Exception innerException = new PipeException(SR.Format(SR.PipeNameCanNotBeAccessed, PipeError.GetErrorString(error)), error);
            return new AddressAccessDeniedException(SR.Format(SR.PipeNameCanNotBeAccessed2, pipeUri.AbsoluteUri), innerException);
        }

        private static string BuildPipeName(string pipeGuid)
        {
            return PipePrefix + pipeGuid;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SharedMemoryContents
        {
            public bool isInitialized;
            public Guid pipeGuid;
        }
    }

    internal static class PipeError
    {
        public static string GetErrorString(int error)
        {
            StringBuilder stringBuilder = new StringBuilder(512);
            if (UnsafeNativeMethods.FormatMessage(UnsafeNativeMethods.FORMAT_MESSAGE_IGNORE_INSERTS |
                UnsafeNativeMethods.FORMAT_MESSAGE_FROM_SYSTEM | UnsafeNativeMethods.FORMAT_MESSAGE_ARGUMENT_ARRAY,
                IntPtr.Zero, error, CultureInfo.CurrentCulture.LCID, stringBuilder, stringBuilder.Capacity, IntPtr.Zero) != 0)
            {
                stringBuilder = stringBuilder.Replace("\n", "");
                stringBuilder = stringBuilder.Replace("\r", "");
                return SR.Format(
                    SR.PipeKnownWin32Error,
                    stringBuilder.ToString(),
                    error.ToString(CultureInfo.InvariantCulture),
                    Convert.ToString(error, 16));
            }
            else
            {
                return SR.Format(
                    SR.PipeUnknownWin32Error,
                    error.ToString(CultureInfo.InvariantCulture),
                    Convert.ToString(error, 16));
            }
        }
    }

    [SupportedOSPlatform("windows")]
    internal static class SecurityDescriptorHelper
    {
        private static byte[] s_worldCreatorOwnerWithReadAndWriteDescriptorDenyNetwork;
        private static byte[] GetWorldCreatorOwnerWithReadAndWriteDescriptorDenyNetwork(ILogger logger)
        {
            if (s_worldCreatorOwnerWithReadAndWriteDescriptorDenyNetwork == null)
            {
                s_worldCreatorOwnerWithReadAndWriteDescriptorDenyNetwork = FromSecurityIdentifiersFull(null, UnsafeNativeMethods.GENERIC_READ | UnsafeNativeMethods.GENERIC_WRITE, logger);
            }

            return s_worldCreatorOwnerWithReadAndWriteDescriptorDenyNetwork;
        }

        private static byte[] s_worldCreatorOwnerWithReadDescriptorDenyNetwork;
        private static byte[] GetWorldCreatorOwnerWithReadDescriptorDenyNetwork(ILogger logger)
        {
            if (s_worldCreatorOwnerWithReadDescriptorDenyNetwork == null)
            {
                s_worldCreatorOwnerWithReadDescriptorDenyNetwork = FromSecurityIdentifiersFull(null, UnsafeNativeMethods.GENERIC_READ, logger);
            }

            return s_worldCreatorOwnerWithReadDescriptorDenyNetwork;
        }

        internal static byte[] FromSecurityIdentifiers(List<SecurityIdentifier> allowedSids, int accessRights, ILogger logger)
        {
            if (allowedSids == null)
            {
                if (accessRights == (UnsafeNativeMethods.GENERIC_READ | UnsafeNativeMethods.GENERIC_WRITE))
                {
                    return GetWorldCreatorOwnerWithReadAndWriteDescriptorDenyNetwork(logger);
                }

                if (accessRights == UnsafeNativeMethods.GENERIC_READ)
                {
                    return GetWorldCreatorOwnerWithReadDescriptorDenyNetwork(logger);
                }
            }

            return FromSecurityIdentifiersFull(allowedSids, accessRights, logger);
        }

        private static byte[] FromSecurityIdentifiersFull(List<SecurityIdentifier> allowedSids, int accessRights, ILogger logger)
        {
            int capacity = allowedSids == null ? 3 : 2 + allowedSids.Count;
            DiscretionaryAcl dacl = new DiscretionaryAcl(false, false, capacity);

            // add deny ACE first so that we don't get short circuited
            dacl.AddAccess(AccessControlType.Deny, new SecurityIdentifier(WellKnownSidType.NetworkSid, null),
                UnsafeNativeMethods.GENERIC_ALL, InheritanceFlags.None, PropagationFlags.None);

            // clients get different rights, since they shouldn't be able to listen
            int clientAccessRights = GenerateClientAccessRights(accessRights);

            if (allowedSids == null)
            {
                logger.LogDebug("Adding default ACL allow WellKnownSidType.WorldSid to security identifiers");
                dacl.AddAccess(AccessControlType.Allow, new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                    clientAccessRights, InheritanceFlags.None, PropagationFlags.None);
            }
            else
            {
                for (int i = 0; i < allowedSids.Count; i++)
                {
                    SecurityIdentifier allowedSid = allowedSids[i];
                    logger.LogDebug("Adding ACL allow SID {allowedSid} to security identifiers", allowedSid);
                    dacl.AddAccess(AccessControlType.Allow, allowedSid,
                        clientAccessRights, InheritanceFlags.None, PropagationFlags.None);
                }
            }

            var processLogonSid = SecurityUtils.GetProcessLogonSid();
            logger.LogDebug("Adding ACL allow Process Logon SID {processLogonSid} to security identifiers", processLogonSid);
            dacl.AddAccess(AccessControlType.Allow, processLogonSid, accessRights, InheritanceFlags.None, PropagationFlags.None);

            // In WCF there is code here to grant access to the current app container SID if running in an
            // AppContainer. The responsibility for this is now going to shift to explicitly configuring
            // in the app via NamedPipeListenOptions. The original functionality was intended for named pipes
            // to be used between processes in the same app container. The reality is that communication
            // into/out of a container is a needed scenario and WCF was getting in the way. Making it
            // needing to be configured explicitly requires a little more work on the originally intended
            // scenario, but enables all scenarios without having to resort to hackery.

            CommonSecurityDescriptor securityDescriptor =
                new CommonSecurityDescriptor(false, false, ControlFlags.None, null, null, null, dacl);
            byte[] binarySecurityDescriptor = new byte[securityDescriptor.BinaryLength];
            securityDescriptor.GetBinaryForm(binarySecurityDescriptor, 0);
            return binarySecurityDescriptor;
        }

        // Security: We cannot grant rights for FILE_CREATE_PIPE_INSTANCE to clients, otherwise other apps can intercept server side pipes.
        // FILE_CREATE_PIPE_INSTANCE is granted in 2 ways, via GENERIC_WRITE or directly specified. Remove both.
        private static int GenerateClientAccessRights(int accessRights)
        {
            int everyoneAccessRights = accessRights;

            if ((everyoneAccessRights & UnsafeNativeMethods.GENERIC_WRITE) != 0)
            {
                everyoneAccessRights &= ~UnsafeNativeMethods.GENERIC_WRITE;

                // Since GENERIC_WRITE grants the permissions to write to a file, we need to add it back.
                const int clientWriteAccess = UnsafeNativeMethods.FILE_WRITE_ATTRIBUTES | UnsafeNativeMethods.FILE_WRITE_DATA | UnsafeNativeMethods.FILE_WRITE_EA;
                everyoneAccessRights |= clientWriteAccess;
            }

            // Future proofing: FILE_CREATE_PIPE_INSTANCE isn't used currently but we need to ensure it is not granted.
            everyoneAccessRights &= ~UnsafeNativeMethods.FILE_CREATE_PIPE_INSTANCE;

            return everyoneAccessRights;
        }
    }
}
