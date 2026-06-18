// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    internal class ByteStreamBufferedMessageData
    {
        private ReadOnlySequence<byte> _buffer;
        private int _refCount;

        public ByteStreamBufferedMessageData(ReadOnlySequence<byte> buffer)
        {
            _buffer = buffer;
            _refCount = 0;
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
                    _buffer = default;
                    _refCount = int.MinValue;
                }
            }
        }

        public Stream ToStream() => PipeReader.Create(ReadOnlyBuffer).AsStream();

        private void ThrowIfClosed()
        {
            if (IsClosed)
            {
                throw Fx.Exception.ObjectDisposed(SR.Format(SR.ObjectDisposed, this));
            }
        }
    }
}
