// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using CoreWCF.Diagnostics;

namespace CoreWCF.Channels
{
    internal sealed class BufferManagerBufferWriter : IBufferWriter<byte>, IDisposable
    {
        private readonly BufferManager _bufferManager;
        private readonly int _maxBufferSize;
        private readonly Func<int, Exception> _quotaExceededExceptionFactory;
        private byte[] _buffer;
        private int _writtenCount;

        public BufferManagerBufferWriter(BufferManager bufferManager, int initialCapacity, int maxBufferSize)
            : this(bufferManager, initialCapacity, maxBufferSize, MaxMessageSizeStream.CreateMaxReceivedMessageSizeExceededException)
        {
        }

        public BufferManagerBufferWriter(BufferManager bufferManager, int initialCapacity, int maxBufferSize, Func<int, Exception> quotaExceededExceptionFactory)
        {
            _bufferManager = bufferManager ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(bufferManager));
            _maxBufferSize = maxBufferSize;
            _quotaExceededExceptionFactory = quotaExceededExceptionFactory ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(quotaExceededExceptionFactory));
            _buffer = bufferManager.TakeBuffer(initialCapacity);
        }

        public int Capacity => _buffer?.Length ?? 0;

        public int WrittenCount => _writtenCount;

        public void Advance(int count)
        {
            ThrowIfDisposed();

            if (count < 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(count), count, SRCommon.ValueMustBeNonNegative));
            }

            if (_writtenCount > _buffer.Length - count)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException("Cannot advance past the end of the buffer."));
            }

            _writtenCount += count;
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            CheckAndResizeBuffer(sizeHint);
            return _buffer.AsMemory(_writtenCount);
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            CheckAndResizeBuffer(sizeHint);
            return _buffer.AsSpan(_writtenCount);
        }

        public ArraySegment<byte> DetachBuffer()
        {
            ThrowIfDisposed();

            byte[] buffer = _buffer;
            _buffer = null;
            return new ArraySegment<byte>(buffer, 0, _writtenCount);
        }

        public void Dispose()
        {
            byte[] buffer = _buffer;
            _buffer = null;
            _writtenCount = 0;

            if (buffer != null)
            {
                _bufferManager.ReturnBuffer(buffer);
            }
        }

        private void CheckAndResizeBuffer(int sizeHint)
        {
            ThrowIfDisposed();

            if (sizeHint < 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(sizeHint), sizeHint, SRCommon.ValueMustBeNonNegative));
            }

            if (sizeHint == 0)
            {
                sizeHint = 1;
            }

            if (_buffer.Length - _writtenCount >= sizeHint)
            {
                return;
            }

            if (sizeHint > _maxBufferSize - _writtenCount)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(_quotaExceededExceptionFactory(_maxBufferSize));
            }

            int newSize = checked(_writtenCount + sizeHint);
            byte[] newBuffer = _bufferManager.TakeBuffer(newSize);
            Buffer.BlockCopy(_buffer, 0, newBuffer, 0, _writtenCount);
            _bufferManager.ReturnBuffer(_buffer);
            _buffer = newBuffer;
        }

        private void ThrowIfDisposed()
        {
            if (_buffer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ObjectDisposedException(nameof(BufferManagerBufferWriter)));
            }
        }
    }

    internal sealed class BufferManagerBufferWriterStream : Stream
    {
        private readonly BufferManagerBufferWriter _writer;

        public BufferManagerBufferWriterStream(string quotaExceededString, int initialSize, int maxSize, BufferManager bufferManager)
            : this(initialSize, maxSize, bufferManager, maxSizeQuota => CreateQuotaExceededException(quotaExceededString, maxSizeQuota))
        {
        }

        public BufferManagerBufferWriterStream(int initialSize, int maxSize, BufferManager bufferManager, Func<int, Exception> quotaExceededExceptionFactory)
        {
            _writer = new BufferManagerBufferWriter(bufferManager, initialSize, maxSize, quotaExceededExceptionFactory);
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => _writer.WrittenCount;

        public override long Position
        {
            get => throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.SeekNotSupported));
            set => throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.SeekNotSupported));
        }

        public void Skip(int size)
        {
            if (size < 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(size), size, SRCommon.ValueMustBeNonNegative));
            }

            if (size == 0)
            {
                return;
            }

            _writer.GetSpan(size);
            _writer.Advance(size);
        }

        public ArraySegment<byte> DetachBuffer() => _writer.DetachBuffer();

        public byte[] ToArray(out int size)
        {
            ArraySegment<byte> buffer = DetachBuffer();
            size = buffer.Count;
            return buffer.Array;
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
            => throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.ReadNotSupported));

        public override long Seek(long offset, SeekOrigin origin)
            => throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.SeekNotSupported));

        public override void SetLength(long value)
            => throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.SeekNotSupported));

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(buffer));
            }

            ValidateWriteParameters(buffer.Length, offset, count);

            if (count == 0)
            {
                return;
            }

            buffer.AsSpan(offset, count).CopyTo(_writer.GetSpan(count));
            _writer.Advance(count);
        }

        public override void WriteByte(byte value)
        {
            Span<byte> span = _writer.GetSpan(1);
            span[0] = value;
            _writer.Advance(1);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _writer.Dispose();
            }

            base.Dispose(disposing);
        }

        private static void ValidateWriteParameters(int bufferLength, int offset, int count)
        {
            if (offset < 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(offset), offset, SRCommon.ValueMustBeNonNegative));
            }

            if (count < 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(count), count, SRCommon.ValueMustBeNonNegative));
            }

            if (bufferLength - offset < count)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.Format(SR.OffsetExceedsBufferSize, bufferLength), nameof(offset)));
            }
        }

        private static Exception CreateQuotaExceededException(string quotaExceededString, int maxSizeQuota)
        {
            string message = SR.Format(quotaExceededString, maxSizeQuota);
            return new QuotaExceededException(message);
        }
    }

    internal static class BufferedMessageStreamHelper
    {
        internal static async Task<ArraySegment<byte>> BufferMessageStreamAsync(Stream stream, BufferManager bufferManager, int maxBufferSize, int initialBufferSize)
        {
            using (BufferManagerBufferWriter writer = new BufferManagerBufferWriter(bufferManager, initialBufferSize, maxBufferSize))
            {
                int currentBufferSize = Math.Min(writer.Capacity, maxBufferSize);

                while (writer.WrittenCount < currentBufferSize)
                {
                    int bytesToRead = currentBufferSize - writer.WrittenCount;
                    Memory<byte> memory = writer.GetMemory(bytesToRead);
                    if (!MemoryMarshal.TryGetArray(memory, out ArraySegment<byte> buffer))
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException("Internal error: BufferManagerBufferWriter should always provide array-backed Memory<byte>."));
                    }

                    int count = await stream.ReadAsync(buffer.Array, buffer.Offset, Math.Min(bytesToRead, buffer.Count));
                    if (count == 0)
                    {
                        stream.Dispose();
                        break;
                    }

                    writer.Advance(count);
                    if (writer.WrittenCount == currentBufferSize)
                    {
                        if (currentBufferSize >= maxBufferSize)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(MaxMessageSizeStream.CreateMaxReceivedMessageSizeExceededException(maxBufferSize));
                        }

                        currentBufferSize = Math.Min(currentBufferSize * 2, maxBufferSize);
                    }
                }

                return writer.DetachBuffer();
            }
        }
    }
}
