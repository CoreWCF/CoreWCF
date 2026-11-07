// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Threading;
using Contract;

namespace Services
{
    public class StreamingService : IStreamingService
    {
        private readonly StreamingState _state;

        public StreamingService(StreamingState state) => _state = state;

        public Stream Download(int length, int pauseAfter) => new GeneratedStream(_state, length, pauseAfter);

        private sealed class GeneratedStream : Stream
        {
            private readonly StreamingState _state;
            private readonly int _length;
            private readonly int _pauseAfter;
            private int _position;

            public GeneratedStream(StreamingState state, int length, int pauseAfter)
            {
                _state = state;
                _length = length;
                _pauseAfter = pauseAfter;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (_pauseAfter > 0 && _position >= _pauseAfter)
                {
                    _state.Paused.Set();
                    if (!_state.Release.Wait(TimeSpan.FromSeconds(30)))
                    {
                        throw new TimeoutException("The streaming test did not release its producer.");
                    }
                }

                int remaining = _length - _position;
                if (_pauseAfter > _position)
                {
                    remaining = Math.Min(remaining, _pauseAfter - _position);
                }
                int bytes = Math.Min(count, remaining);
                for (int i = 0; i < bytes; i++)
                {
                    buffer[offset + i] = (byte)((_position + i) % 251);
                }
                _position += bytes;
                Interlocked.Add(ref _state.BytesRead, bytes);
                return bytes;
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => _length;
            public override long Position { get => _position; set => throw new NotSupportedException(); }
            public override void Flush() => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        }
    }

    public sealed class StreamingState : IDisposable
    {
        public long BytesRead;
        public ManualResetEventSlim Paused { get; } = new ManualResetEventSlim();
        public ManualResetEventSlim Release { get; } = new ManualResetEventSlim();

        public void Dispose()
        {
            Paused.Dispose();
            Release.Dispose();
        }
    }
}
