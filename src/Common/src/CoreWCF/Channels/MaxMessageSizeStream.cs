// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Channels
{
    internal class MaxMessageSizeStream : DelegatingStream
    {
        private readonly long _maxMessageSize;
        private long _totalBytesRead;
        private long _bytesWritten;

        public MaxMessageSizeStream(Stream stream, long maxMessageSize)
            : base(stream)
        {
            _maxMessageSize = maxMessageSize;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            count = PrepareRead(count);
            int bytesRead = await base.ReadAsync(buffer, offset, count, cancellationToken);
            return FinishRead(bytesRead);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            PrepareWrite(count);
            return base.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            count = PrepareRead(count);
            return FinishRead(base.Read(buffer, offset, count));
        }

        public override int ReadByte()
        {
            PrepareRead(1);
            int i = base.ReadByte();
            if (i != -1)
            {
                FinishRead(1);
            }

            return i;
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            PrepareWrite(count);
            base.Write(buffer, offset, count);
        }

        public override void WriteByte(byte value)
        {
            PrepareWrite(1);
            base.WriteByte(value);
        }

        public static Exception CreateMaxReceivedMessageSizeExceededException(long maxMessageSize)
        {
            string message = SRCommon.Format(SRCommon.MaxReceivedMessageSizeExceeded, maxMessageSize);
            Exception inner = new QuotaExceededException(message);

            return new CommunicationException(message, inner);
        }

        internal static Exception CreateMaxSentMessageSizeExceededException(long maxMessageSize)
        {
            string message = SRCommon.Format(SRCommon.MaxSentMessageSizeExceeded, maxMessageSize);
            Exception inner = new QuotaExceededException(message);

            return new CommunicationException(message, inner);
        }

        private int PrepareRead(int bytesToRead)
        {
            if (_totalBytesRead >= _maxMessageSize)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateMaxReceivedMessageSizeExceededException(_maxMessageSize));
            }

            long bytesRemaining = _maxMessageSize - _totalBytesRead;

            if (bytesRemaining > int.MaxValue)
            {
                return bytesToRead;
            }
            else
            {
                return Math.Min(bytesToRead, (int)(_maxMessageSize - _totalBytesRead));
            }
        }

        private int FinishRead(int bytesRead)
        {
            _totalBytesRead += bytesRead;
            return bytesRead;
        }

        private void PrepareWrite(int bytesToWrite)
        {
            if (_bytesWritten + bytesToWrite > _maxMessageSize)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateMaxSentMessageSizeExceededException(_maxMessageSize));
            }

            _bytesWritten += bytesToWrite;
        }
    }
}
