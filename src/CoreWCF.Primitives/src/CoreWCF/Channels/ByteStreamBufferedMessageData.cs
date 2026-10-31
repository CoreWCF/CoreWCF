// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Runtime.InteropServices;
using System.IO;
using System.IO.Pipelines;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    internal class ByteStreamBufferedMessageData
    {
        private ReadOnlySequence<byte> _buffer;
        private BufferManager _bufferManager;
        private ReadOnlySequence<byte> _rentedBuffer;
        private int _refCount;

        public ByteStreamBufferedMessageData(ReadOnlySequence<byte> buffer)
        {
            _buffer = buffer;
            _refCount = 0;
        }

        /// <summary>
        /// Hands ownership of <paramref name="rentedBuffer"/> to this instance: the array behind it
        /// goes back to <paramref name="bufferManager"/> once the last reference is released.
        /// Callers reading out of memory they don't own never call this.
        /// </summary>
        public void OwnBuffer(ReadOnlySequence<byte> rentedBuffer, BufferManager bufferManager)
        {
            if (bufferManager != null && !rentedBuffer.IsEmpty)
            {
                _rentedBuffer = rentedBuffer;
                _bufferManager = bufferManager;
            }
        }

        private void ReturnRentedBuffer()
        {
            if (_bufferManager == null)
            {
                return;
            }

            // One array per segment: a message received in pieces is chained rather than copied
            // into a single buffer, and every piece came from the manager.
            foreach (ReadOnlyMemory<byte> memory in _rentedBuffer)
            {
                if (MemoryMarshal.TryGetArray(memory, out ArraySegment<byte> segment) && segment.Array != null)
                {
                    _bufferManager.ReturnBuffer(segment.Array);
                }
            }

            _bufferManager = null;
            _rentedBuffer = default;
        }

        private bool IsClosed => _refCount < 0;

        public ReadOnlySequence<byte> ReadOnlyBuffer
        {
            get
            {
                ThrowIfClosed();
                return _buffer;
            }
        }

        public void Open()
        {
            ThrowIfClosed();
            _refCount++;
        }

        public void Close()
        {
            if (!IsClosed)
            {
                if (--_refCount <= 0)
                {
                    ReturnRentedBuffer();
                    _buffer = default;
                    _refCount = int.MinValue;
                }
            }
        }

        public Stream ToStream() => new ByteStreamBufferedMessageDataStream(this);

        private void ThrowIfClosed()
        {
            if (IsClosed)
            {
                throw Fx.Exception.ObjectDisposed(SR.Format(SR.ObjectDisposed, this));
            }
        }

        // Holds a reference on the message data for as long as the body stream is in use, so the
        // buffer it reads from isn't handed back to the BufferManager underneath it.
        private sealed class ByteStreamBufferedMessageDataStream : DelegatingStream
        {
            private readonly ByteStreamBufferedMessageData _messageData;
            private bool _closed;

            public ByteStreamBufferedMessageDataStream(ByteStreamBufferedMessageData messageData)
                : base(CreateStream(messageData.ReadOnlyBuffer))
            {
                _messageData = messageData;
                _messageData.Open();
            }

            private static Stream CreateStream(ReadOnlySequence<byte> buffer)
            {
                // A single array backed segment keeps the seekable stream callers have always got
                // back from GetBody<Stream>(); a sequence spanning segments can only be read
                // forward without copying it out first.
                if (buffer.IsSingleSegment
                    && MemoryMarshal.TryGetArray(buffer.First, out ArraySegment<byte> segment)
                    && segment.Array != null)
                {
                    return new MemoryStream(segment.Array, segment.Offset, segment.Count, writable: false);
                }

                return PipeReader.Create(buffer).AsStream();
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing && !_closed)
                {
                    _closed = true;
                    _messageData.Close();
                }

                base.Dispose(disposing);
            }
        }
    }
}
