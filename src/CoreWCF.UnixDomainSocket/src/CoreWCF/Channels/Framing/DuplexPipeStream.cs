// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Runtime;

namespace CoreWCF.Channels.Framing
{
    internal class DuplexPipeStream : Stream, IAsyncDisposable
    {
        private readonly PipeReader _input;
        private readonly PipeWriter _output;
        private IInputLengthDecider _inputLengthDecider;

        public DuplexPipeStream(PipeReader input, PipeWriter output)
        {
            _input = input;
            _output = output;
            _inputLengthDecider = NoopInputLengthDecider.Instance;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get
            {
                throw new NotSupportedException();
            }
            set
            {
                throw new NotSupportedException();
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ValueTask<int> vt = ReadAsyncInternal(new Memory<byte>(buffer, offset, count), default);
            return vt.IsCompleted ?
                vt.Result :
                vt.AsTask().GetAwaiter().GetResult();
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken = default)
        {
            return ReadAsyncInternal(new Memory<byte>(buffer, offset, count), cancellationToken).AsTask();
        }

        // TODO: Enable code when moving to .NET 5
        //public override ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken = default)
        //{
        //    return ReadAsyncInternal(destination, cancellationToken);
        //}

        public override void Write(byte[] buffer, int offset, int count)
        {
            WriteAsync(buffer, offset, count).GetAwaiter().GetResult();
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return _output.WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
        }

        // TODO: Enable code when moving to .NET 5
        //public override ValueTask WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default)
        //{
        //    return _output.WriteAsync(source, cancellationToken).GetAsValueTask();
        //}

        public override void Flush()
        {
            FlushAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return _output.FlushAsync(cancellationToken).AsTask();
        }

        private async ValueTask<int> ReadAsyncInternal(Memory<byte> destination, CancellationToken cancellationToken)
        {
            while (true)
            {
                var result = await _input.ReadAsync(cancellationToken);
                var readableBuffer = result.Buffer;
                try
                {
                    if (!readableBuffer.IsEmpty)
                    {
                        // buffer.Count is int
                        var count = _inputLengthDecider.LengthToConsume(readableBuffer, destination.Length);
                        readableBuffer = readableBuffer.Slice(0, count);
                        readableBuffer.CopyTo(destination.Span);
                        return count;
                    }

                    if (result.IsCompleted)
                    {
                        return 0;
                    }
                }
                finally
                {
                    _input.AdvanceTo(readableBuffer.End, readableBuffer.End);
                }
            }
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return ReadAsync(buffer, offset, count).ToApm(callback, state);
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            return asyncResult.ToApmEnd<int>();
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return WriteAsync(buffer, offset, count).ToApm(callback, state);
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            asyncResult.ToApmEnd();
        }

        protected override void Dispose(bool disposing)
        {
            _input.Complete();
            _output.Complete();
            base.Dispose(disposing);
        }

        public async ValueTask DisposeAsync()
        {
            await _input.CompleteAsync();
            await _output.CompleteAsync();
            base.Dispose(true);
        }

        internal void SetContentType(string contentType)
        {
            if (contentType == "application/ssl-tls" /*FramingUpgradeString.SslOrTls*/)
                _inputLengthDecider = new TlsInputLengthDecider();
            else _inputLengthDecider = NoopInputLengthDecider.Instance;
        }

        internal interface IInputLengthDecider
        {
            int LengthToConsume(ReadOnlySequence<byte> buffer, int destinationLength);
        }

        internal class TlsInputLengthDecider : IInputLengthDecider
        {
            // buffer[0] is TLS Frame content type, eg 23 = ApplicationData
            // buffer[1] and buffer[2] are the TLS version, eg 0x0303 == TLS 1.2
            // buffer[3] and buffer[4] and big endian integer for length

            private int _frameSize;

            public int LengthToConsume(ReadOnlySequence<byte> buffer, int destinationLength)
            {
                // Maximum number of bytes that can be read from incoming buffer. This is either the entire buffer if there's
                // space in the destination, or however much space is available in the destination buffer
                int maxRead = (int)Math.Min(buffer.Length, destinationLength);
                int bytesToRead;

                // If there's still bytes left to be read from the current frame, we don't need to read the frame header. Only
                // read the frame size if we've finished reading the previous frame.
                if (_frameSize == 0)
                {
                    if (buffer.Length < 5)
                    {
                        // Need at least 5 bytes to read size from frame header so presuming this isn't a TLS frame
                        // so indicating to consume everything that's possible.
                        return maxRead;
                    }

                    if (buffer.IsSingleSegment || buffer.First.Length >= 5)
                    {
                        // We have enough bytes to read the frame size from the first segment
                        var span = buffer.First.Span;
                        Fx.Assert(span[1] == 3, "Invalid TLS header");
                        // The length in the TLS header doesn't include the header itself so that needs to be added
                        _frameSize = ((span[3] << 8) | span[4]) + 5;
                    }
                    else
                    {
                        // We have at least 5 bytes, but the first 5 bytes aren't in the first segment so get the 5 bytes the slow way.
                        var frameHeaderBytes = buffer.Slice(0, 5).ToArray();
                        // The length in the TLS header doesn't include the header itself so that needs to be added
                        _frameSize = ((frameHeaderBytes[3] << 8) | frameHeaderBytes[4]) + 5;
                    }
                }

                // If the frame size is smaller than the number of bytes read from the input, then only read frame size number of bytes.
                bytesToRead = Math.Min(maxRead, _frameSize);
                // Decrement _frameSize by bytesToRead to store number of bytes still pending to be read in future reads.
                _frameSize -= bytesToRead;
                return bytesToRead;
            }
        }

        internal class NoopInputLengthDecider : IInputLengthDecider
        {
            internal static IInputLengthDecider Instance = new NoopInputLengthDecider();
            public int LengthToConsume(ReadOnlySequence<byte> buffer, int destinationLength)
            {
                // Can't read more than there's space in the destination buffer
                return (int)Math.Min(buffer.Length, destinationLength);
            }
        }
    }
}
