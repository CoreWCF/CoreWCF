// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Channels.Framing
{
    internal class UnixDomainSocketExceptionConvertingDuplexPipe : IDuplexPipe
    {
        public UnixDomainSocketExceptionConvertingDuplexPipe(IDuplexPipe innerDuplexPipe)
        {
            Input = new UnixDomainSocketExceptionConvertingPipeReader(innerDuplexPipe.Input);
            Output = new UnixDomainSocketExceptionConvertingPipeWriter(innerDuplexPipe.Output);
        }

        public PipeReader Input { get; }
        public PipeWriter Output { get; }

        private class UnixDomainSocketExceptionConvertingPipeReader : PipeReader
        {
            private PipeReader _input;

            public UnixDomainSocketExceptionConvertingPipeReader(PipeReader input) => _input = input;

            public override void AdvanceTo(SequencePosition consumed) => _input.AdvanceTo(consumed);
            public override void AdvanceTo(SequencePosition consumed, SequencePosition examined) => _input.AdvanceTo(consumed, examined);
            public override void CancelPendingRead() => _input.CancelPendingRead();
            public override void Complete(Exception exception = null) => _input.Complete(exception);
            public override async ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default)
            {
                try
                {
                    return await _input.ReadAsync(cancellationToken);
                }
                catch(SocketException socketException)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelper(
                        ConvertReceiveException(socketException), TraceEventType.Error);
                }
            }

            public override bool TryRead(out ReadResult result) => _input.TryRead(out result);

            private Exception ConvertReceiveException(SocketException socketException)
            {
                return ConvertTransferException(socketException, socketException, aborted: false);
            }
        }

        private class UnixDomainSocketExceptionConvertingPipeWriter : PipeWriter
        {
            private PipeWriter _output;

            public UnixDomainSocketExceptionConvertingPipeWriter(PipeWriter output) => _output = output;

            public override void Advance(int bytes) => _output.Advance(bytes);
            public override void CancelPendingFlush() => _output.CancelPendingFlush();
            public override void Complete(Exception exception = null) => _output.Complete(exception);
            public override async ValueTask<FlushResult> FlushAsync(CancellationToken cancellationToken = default)
            {
                try
                {
                    return await _output.FlushAsync(cancellationToken);
                }
                catch (SocketException socketException)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelper(
                        ConvertSendException(socketException), TraceEventType.Error);
                }
            }

            public override Memory<byte> GetMemory(int sizeHint = 0) => _output.GetMemory(sizeHint);
            public override Span<byte> GetSpan(int sizeHint = 0) => _output.GetSpan(sizeHint);

            private Exception ConvertSendException(SocketException socketException)
            {
                return ConvertTransferException(socketException, socketException, aborted: false);
            }
        }

        private static Exception ConvertTransferException(SocketException socketException, Exception originalException, bool aborted)
        {
            if (socketException.ErrorCode == UnsafeNativeMethods.ERROR_INVALID_HANDLE)
            {
                return new CommunicationObjectAbortedException(socketException.Message, socketException);
            }

            if (socketException.ErrorCode == UnsafeNativeMethods.WSAENETRESET ||
                socketException.ErrorCode == UnsafeNativeMethods.WSAECONNABORTED ||
                socketException.ErrorCode == UnsafeNativeMethods.WSAECONNRESET)
            {
                if (aborted)
                {
                    return new CommunicationObjectAbortedException(SR.TcpLocalConnectionAborted, originalException);
                }
                else
                {
                    CommunicationException communicationException = new CommunicationException(SR.Format(SR.TcpConnectionResetError, "unknown"), originalException);
                    //if (TD.TcpConnectionResetErrorIsEnabled())
                    //{
                    //    if (socketConnection != null)
                    //    {
                    //        int socketId = (socketConnection.socket != null) ? socketConnection.socket.GetHashCode() : -1;
                    //        TD.TcpConnectionResetError(socketId, socketConnection.RemoteEndpointAddress);
                    //    }
                    //}
                    //if (DiagnosticUtility.ShouldTrace(exceptionEventType))
                    //{
                    //    TraceUtility.TraceEvent(exceptionEventType, TraceCode.TcpConnectionResetError, GetEndpointString(SR.TcpConnectionResetError, timeout, null, socketConnection), communicationException, null);
                    //}
                    return communicationException;
                }
            }
            else if (socketException.ErrorCode == UnsafeNativeMethods.WSAETIMEDOUT)
            {
                TimeoutException timeoutException = new TimeoutException(SR.Format(SR.TcpConnectionTimedOut, "unknown"), originalException);
                //if (DiagnosticUtility.ShouldTrace(exceptionEventType))
                //{
                //    TraceUtility.TraceEvent(exceptionEventType, TraceCode.TcpConnectionTimedOut, GetEndpointString(SR.TcpConnectionTimedOut, timeout, null, socketConnection), timeoutException, null);
                //}
                return timeoutException;
            }
            else
            {
                if (aborted)
                {
                    return new CommunicationObjectAbortedException(SR.Format(SR.TcpTransferError, socketException.ErrorCode, socketException.Message), originalException);
                }
                else
                {
                    CommunicationException communicationException = new CommunicationException(SR.Format(SR.TcpTransferError, socketException.ErrorCode, socketException.Message), originalException);
                    //if (DiagnosticUtility.ShouldTrace(exceptionEventType))
                    //{
                    //    TraceUtility.TraceEvent(exceptionEventType, TraceCode.TcpTransferError, GetEndpointString(SR.TcpTransferError, TimeSpan.MinValue, socketException, socketConnection), communicationException, null);
                    //}
                    return communicationException;
                }
            }
        }

        internal static class UnsafeNativeMethods
        {
            public const int ERROR_SUCCESS = 0;
            public const int ERROR_FILE_NOT_FOUND = 2;
            public const int ERROR_ACCESS_DENIED = 5;
            public const int ERROR_INVALID_HANDLE = 6;
            public const int ERROR_NOT_ENOUGH_MEMORY = 8;
            public const int ERROR_OUTOFMEMORY = 14;
            public const int ERROR_SHARING_VIOLATION = 32;
            public const int ERROR_NETNAME_DELETED = 64;
            public const int ERROR_INVALID_PARAMETER = 87;
            public const int ERROR_BROKEN_PIPE = 109;
            public const int ERROR_ALREADY_EXISTS = 183;
            public const int ERROR_PIPE_BUSY = 231;
            public const int ERROR_NO_DATA = 232;
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
            private const int ERROR_INSUFFICIENT_BUFFER = 122;

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
        }
    }
}
