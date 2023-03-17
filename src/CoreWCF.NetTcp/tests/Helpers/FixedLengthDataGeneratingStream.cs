// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;

namespace Helpers
{
    internal class FixedLengthDataGeneratingStream : Stream
    {
        public FixedLengthDataGeneratingStream(long size)
        {
            Length = size;
        }
        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length { get; }
        public override long Position { get; set; }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int bytesToRead = (int)Math.Min(count, Length - Position);
            if (bytesToRead == 0) return 0;
            Span<byte> bytes = new Span<byte>(buffer, offset, bytesToRead);
            bytes.Fill((byte)'A');
            Position += bytesToRead;
            return bytesToRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    {
                        if (offset > Length || offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
                        Position = offset;
                        break;
                    }
                case SeekOrigin.Current:
                    {
                        long tempPosition = unchecked(Position + offset);
                        if (tempPosition < 0 || tempPosition > Length)
                            throw new IOException("Either overflowed long or trying to seek past end of stream");
                        Position = tempPosition;
                        break;
                    }
                case SeekOrigin.End:
                    {
                        long tempPosition = unchecked(Length + offset);
                        if (tempPosition < 0 || tempPosition > Length)
                            throw new IOException("Either underflowed long or trying to seek before start of stream");
                        Position = tempPosition;
                        break;
                    }
                default:
                    throw new ArgumentException("Invalid SeekOrigin value", nameof(origin));
            }
            return Position;
        }

        public override void SetLength(long value) => throw new InvalidOperationException();
        public override void Write(byte[] buffer, int offset, int count) => throw new InvalidOperationException();
    }
}
