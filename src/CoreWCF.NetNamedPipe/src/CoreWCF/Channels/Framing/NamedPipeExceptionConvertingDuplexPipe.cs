// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.IO.Pipelines;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Security;

namespace CoreWCF.Channels.Framing
{
    // TODO: Implement the exception conversion
    internal partial class NamedPipeExceptionConvertingDuplexPipe : IDuplexPipe
    {
        public NamedPipeExceptionConvertingDuplexPipe(IDuplexPipe innerDuplexPipe)
        {
            Input = new NamedPipeExceptionConvertingPipeReader(innerDuplexPipe.Input);
            Output = new NamedPipeExceptionConvertingPipeWriter(innerDuplexPipe.Output);
        }

        public PipeReader Input { get; }
        public PipeWriter Output { get; }

        private class NamedPipeExceptionConvertingPipeReader : PipeReader
        {
            private PipeReader _input;

            public NamedPipeExceptionConvertingPipeReader(PipeReader input) => _input = input;

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

        private class NamedPipeExceptionConvertingPipeWriter : PipeWriter
        {
            private PipeWriter _output;

            public NamedPipeExceptionConvertingPipeWriter(PipeWriter output) => _output = output;

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
    }
}
