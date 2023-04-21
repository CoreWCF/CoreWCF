// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.IO;
using CoreWCF.Runtime;
using CoreWCF.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Win32.SafeHandles;

namespace CoreWCF.Channels
{
    [SupportedOSPlatform("windows")]
    internal static class PipeStreamHelper
    {
        internal static readonly Action<object> s_cancellationCallback = CancellationCallback;

        public static NamedPipeServerStream CreatePipeStream(NamedPipeListenOptions options, string pipeName, ref bool firstConnection)
        {
            var handle = CreatePipe(options, pipeName, ref firstConnection);
            return new NamedPipeServerStream(PipeDirection.InOut, isAsync: true, isConnected: false, handle);
        }

        public static unsafe Task WriteZeroAsync(this NamedPipeServerStream pipeStream, CancellationToken cancellationToken)
        {
            byte[] zeroByteBuffer = new byte[0];
            Overlapped overlapped = new Overlapped();
            var zeroByteGCHandle = GCHandle.Alloc(zeroByteBuffer, GCHandleType.Pinned);
            var stateHolder = new StateHolder(zeroByteGCHandle);
            CancellationTokenRegistration cancellationRegistration = default;
            var nativeOverlapped = overlapped.Pack(IOCallback, stateHolder);
            // Queue an async WriteFile operation.
            if (UnsafeNativeMethods.WriteFile(pipeStream.SafePipeHandle, ref zeroByteBuffer, 0, IntPtr.Zero, nativeOverlapped) == 0)
            {
                // The operation failed, or it's pending.
                int error = Marshal.GetLastWin32Error();
                switch (error)
                {
                    case UnsafeNativeMethods.ERROR_IO_PENDING:
                        // Common case: IO was initiated, completion will be handled by callback.
                        // Register for cancellation now that the operation has been initiated.
                        cancellationRegistration = cancellationToken.Register(s_cancellationCallback, (stateHolder.TaskCompletionSource, cancellationToken));
                        // Need to cleanup cancellation registration after the Task completes.
                        return stateHolder.TaskCompletionSource.Task.ContinueWith((task, state) => { ((CancellationTokenRegistration)state).Dispose(); }, cancellationRegistration);
                    default:
                        // Error. Callback will not be invoked.
                        Overlapped.Unpack(nativeOverlapped);
                        zeroByteGCHandle.Free();
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateWriteException(error));
                }
            }
            else
            {
                // WriteFile returned a non-zero result which means it completed execution synchronously.
                // Need to cleanup the zero byte memory handle, but not the cancellation registration
                // as that only gets registered when we go async.
                stateHolder.TaskCompletionSource.TrySetResult(null);
                zeroByteGCHandle.Free();
                Overlapped.Unpack(nativeOverlapped);
                return stateHolder.TaskCompletionSource.Task;
            }
        }

        private static unsafe void IOCallback(uint errorCode, uint numBytes, NativeOverlapped* pOverlapped)
        {
            Overlapped overlapped = Overlapped.Unpack(pOverlapped);
            var stateHolder = (StateHolder)overlapped.AsyncResult;
            if (errorCode != 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateWriteException((int)errorCode));
            }
            stateHolder.GCHandle.Free();
            stateHolder.TaskCompletionSource.TrySetResult(null);
        }

        private static void CancellationCallback(object obj)
        {
            var state = ((TaskCompletionSource<object> tcs, CancellationToken cancellationToken))obj;
            state.tcs.TrySetCanceled(state.cancellationToken);
        }

        private static PipeException CreateWriteException(int error)
        {
            return CreateException(SR.PipeWriteError, error);
        }

        private static PipeException CreateException(string resourceString, int error)
        {
            return new PipeException(SR.Format(resourceString, PipeError.GetErrorString(error)), error);
        }

        private class StateHolder : IAsyncResult
        {
            public StateHolder(GCHandle gcHandle)
            {
                GCHandle = gcHandle;
            }

            public TaskCompletionSource<object> TaskCompletionSource { get; } = new TaskCompletionSource<object>();
            public GCHandle GCHandle { get; }
            public object AsyncState => throw Fx.AssertAndThrow("StateHolder.AsyncState called.");
            public WaitHandle AsyncWaitHandle => throw Fx.AssertAndThrow("StateHolder.AsyncWaitHandle called.");
            public bool CompletedSynchronously => throw Fx.AssertAndThrow("StateHolder.CompletedSynchronously called.");
            public bool IsCompleted => throw Fx.AssertAndThrow("StateHolder.IsCompleted called.");
        }

        private static SafePipeHandle CreatePipe(NamedPipeListenOptions options, string pipeName, ref bool firstConnection)
        {
            var loggerFactory = options.ApplicationServices.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger(typeof(PipeStreamHelper).FullName);
            int openMode = UnsafeNativeMethods.PIPE_ACCESS_DUPLEX | UnsafeNativeMethods.FILE_FLAG_OVERLAPPED;
            if (firstConnection)
            {
                openMode |= UnsafeNativeMethods.FILE_FLAG_FIRST_PIPE_INSTANCE;
            }

            byte[] binarySecurityDescriptor;

            try
            {
                binarySecurityDescriptor = SecurityDescriptorHelper.FromSecurityIdentifiers(options.InternalAllowedUsers, UnsafeNativeMethods.GENERIC_READ | UnsafeNativeMethods.GENERIC_WRITE, logger);
            }
            catch (Win32Exception e)
            {
                // While Win32exceptions are not expected, if they do occur we need to obey the pipe/communication exception model.
                Exception innerException = new PipeException(e.Message, e);
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CommunicationException(innerException.Message, innerException));
            }

            SafePipeHandle pipeHandle;
            GCHandle binarySecurityDescriptorHandle = default;
            int error;
            try
            {
                binarySecurityDescriptorHandle = GCHandle.Alloc(binarySecurityDescriptor, GCHandleType.Pinned);
                UnsafeNativeMethods.SECURITY_ATTRIBUTES securityAttributes = new UnsafeNativeMethods.SECURITY_ATTRIBUTES();
                // TODO: Try replacing lpSecurityDescriptor with byte[]. I think we can avoid pinning.
                securityAttributes.lpSecurityDescriptor = binarySecurityDescriptorHandle.AddrOfPinnedObject();

                pipeHandle = UnsafeNativeMethods.CreateNamedPipe(
                                                    pipeName,
                                                    openMode,
                                                    UnsafeNativeMethods.PIPE_TYPE_MESSAGE | UnsafeNativeMethods.PIPE_READMODE_MESSAGE,
                                                    UnsafeNativeMethods.PIPE_UNLIMITED_INSTANCES,
                                                    options.ConnectionBufferSize,
                                                    options.ConnectionBufferSize, 0, securityAttributes);
                error = Marshal.GetLastWin32Error();
            }
            finally
            {
                if (binarySecurityDescriptorHandle.IsAllocated)
                {
                    binarySecurityDescriptorHandle.Free();
                }
            }

            if (pipeHandle.IsInvalid)
            {
                pipeHandle.SetHandleAsInvalid();

                Exception innerException = new PipeException(SR.Format(SR.PipeListenFailed,
                    options.BaseAddress.AbsoluteUri, PipeError.GetErrorString(error)), error);

                if (error == UnsafeNativeMethods.ERROR_ACCESS_DENIED)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new AddressAccessDeniedException(innerException.Message, innerException));
                }
                else if (error == UnsafeNativeMethods.ERROR_ALREADY_EXISTS)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new AddressAlreadyInUseException(innerException.Message, innerException));
                }
                else
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CommunicationException(innerException.Message, innerException));
                }
            }
            //else
            //{
            //    if (TD.NamedPipeCreatedIsEnabled())
            //    {
            //        TD.NamedPipeCreated(pipeName);
            //    }
            //}

            firstConnection = false;
            return pipeHandle;
        }

    }
}
