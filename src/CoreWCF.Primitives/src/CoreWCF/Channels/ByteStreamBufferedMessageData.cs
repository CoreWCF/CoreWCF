// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    internal class ByteStreamBufferedMessageData
    {
        private ArraySegment<byte> _buffer;
        private BufferManager _bufferManager;
        private int _refCount;

        public ByteStreamBufferedMessageData(ArraySegment<byte> buffer)
            : this(buffer, null)
        {
        }

        public ByteStreamBufferedMessageData(ArraySegment<byte> buffer, BufferManager bufferManager)
        {
            if (buffer.Array == null)
            {
                throw Fx.Exception.ArgumentNull(SR.Format(SR.ArgumentPropertyShouldNotBeNullError, "buffer.Array"));
            }

            _buffer = buffer;
            _bufferManager = bufferManager;
            _refCount = 0;
        }

        private bool IsClosed => _refCount < 0;

        public ArraySegment<byte> Buffer
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
                    if (_bufferManager != null && _buffer.Array != null)
                    {
                        _bufferManager.ReturnBuffer(_buffer.Array);
                    }

                    _bufferManager = null;
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

        internal class ByteStreamBufferedMessageDataStream : MemoryStream
        {
            private readonly ByteStreamBufferedMessageData _byteStreamBufferedMessageData;

            public ByteStreamBufferedMessageDataStream(ByteStreamBufferedMessageData byteStreamBufferedMessageData)
                : base(byteStreamBufferedMessageData.Buffer.Array, byteStreamBufferedMessageData.Buffer.Offset, byteStreamBufferedMessageData.Buffer.Count, false)
            {
                _byteStreamBufferedMessageData = byteStreamBufferedMessageData;
                _byteStreamBufferedMessageData.Open(); //increment the refCount
            }

            public override void Close()
            {
                _byteStreamBufferedMessageData.Close();
                base.Close();
            }
        }
    }
}
