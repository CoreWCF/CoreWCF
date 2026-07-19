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
        private byte[] _buffer;
        private int _writtenCount;

        public BufferManagerBufferWriter(BufferManager bufferManager, int initialCapacity, int maxBufferSize)
        {
            _bufferManager = bufferManager ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(bufferManager));
            _maxBufferSize = maxBufferSize;
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
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException());
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
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(MaxMessageSizeStream.CreateMaxReceivedMessageSizeExceededException(_maxBufferSize));
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
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException());
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
