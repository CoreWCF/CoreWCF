// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.IO;
using System.IO.Pipelines;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Runtime;
using Microsoft.Extensions.Logging;

namespace CoreWCF.Channels.Framing
{
    internal class LoggingDuplexPipe : DuplexPipeStreamAdapter<LoggingStream>
    {
        public LoggingDuplexPipe(IDuplexPipe transport, ILogger logger) :
            base(transport, stream => new LoggingStream(stream, logger))
        {
        }

        public bool LoggingEnabled
        {
            get => Stream.LoggingEnabled;
            internal set => Stream.LoggingEnabled = value;
        }
    }

    internal class DuplexPipeStreamAdapter<TStream> : DuplexPipeStream, IDuplexPipe where TStream : Stream
    {
        public DuplexPipeStreamAdapter(IDuplexPipe duplexPipe, Func<Stream, TStream> createStream) :
            this(duplexPipe, new StreamPipeReaderOptions(leaveOpen: false), new StreamPipeWriterOptions(leaveOpen: false), createStream)
        {
        }

        public DuplexPipeStreamAdapter(IDuplexPipe duplexPipe, StreamPipeReaderOptions readerOptions, StreamPipeWriterOptions writerOptions, Func<Stream, TStream> createStream) : base(duplexPipe.Input, duplexPipe.Output)
        {
            Stream = createStream(this);
            Input = PipeReader.Create(Stream, readerOptions);
            Output = PipeWriter.Create(Stream, writerOptions);
        }

        public TStream Stream { get; }

        public PipeReader Input { get; }

        public PipeWriter Output { get; }

        protected override void Dispose(bool disposing)
        {
            Input.Complete();
            Output.Complete();
            base.Dispose(disposing);
        }

        //public override ValueTask DisposeAsync()
        //{
        //    Input.Complete();
        //    Output.Complete();
        //    return base.DisposeAsync();
        //}
    }

    internal sealed class LoggingStream : Stream
    {
        private readonly Stream _inner;
        private readonly ILogger _logger;

        public LoggingStream(Stream inner, ILogger logger)
        {
            _inner = inner;
            _logger = logger;
        }

        public override bool CanRead => _inner.CanRead;

        public override bool CanSeek => _inner.CanSeek;

        public override bool CanWrite => _inner.CanWrite;

        public override long Length => _inner.Length;

        public override long Position
        {
            get => _inner.Position;
            set => _inner.Position = value;
        }

        public bool LoggingEnabled { get; internal set; }

        public override void Flush() => _inner.Flush();

        public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);

        public override int Read(byte[] buffer, int offset, int count)
        {
            int read = _inner.Read(buffer, offset, count);
            Log("Read", new ReadOnlySpan<byte>(buffer, offset, read));
            return read;
        }

        // TODO: Enable code when moving to .NET 5
        //public override int Read(Span<byte> destination)
        //{
        //    int read = _inner.Read(destination);
        //    Log("Read", destination.Slice(0, read));
        //    return read;
        //}

        public async override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            int read = await _inner.ReadAsync(buffer, offset, count, cancellationToken);
            Log("ReadAsync", new ReadOnlySpan<byte>(buffer, offset, read));
            return read;
        }

        // TODO: Enable code when moving to .NET 5
        //public override async ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken = default)
        //{
        //    int read = await _inner.ReadAsync(destination, cancellationToken);
        //    Log("ReadAsync", destination.Span.Slice(0, read));
        //    return read;
        //}

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _inner.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            _inner.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Log("Write", new ReadOnlySpan<byte>(buffer, offset, count));
            _inner.Write(buffer, offset, count);
        }

        // TODO: Enable code when moving to .NET 5
        //public override void Write(ReadOnlySpan<byte> source)
        //{
        //    Log("Write", source);
        //    _inner.Write(source);
        //}

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            Log("WriteAsync", new ReadOnlySpan<byte>(buffer, offset, count));
            return _inner.WriteAsync(buffer, offset, count, cancellationToken);
        }

        // TODO: Enable code when moving to .NET 5
        //public override ValueTask WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default)
        //{
        //    Log("WriteAsync", source.Span);
        //    return _inner.WriteAsync(source, cancellationToken);
        //}

        private void Log(string method, ReadOnlySpan<byte> buffer)
        {
            if (!LoggingEnabled || !_logger.IsEnabled(LogLevel.Debug))
            {
                return;
            }

            var builder = new StringBuilder();
            //builder.Append(method);
            //builder.Append('[');
            //builder.Append(buffer.Length);
            //builder.Append(']');

            if (buffer.Length > 0)
            {
                builder.AppendLine();
            }

            var charBuilder = new StringBuilder();

            // Write the hex
            for (int i = 0; i < buffer.Length; i++)
            {
                builder.Append(buffer[i].ToString("X2", CultureInfo.InvariantCulture));
                builder.Append(' ');

                var bufferChar = (char)buffer[i];
                if (char.IsControl(bufferChar))
                {
                    charBuilder.Append('.');
                }
                else
                {
                    charBuilder.Append(bufferChar);
                }

                if ((i + 1) % 16 == 0)
                {
                    builder.Append("  ");
                    builder.Append(charBuilder);
                    if (i != buffer.Length - 1)
                    {
                        builder.AppendLine();
                    }
                    charBuilder.Clear();
                }
                else if ((i + 1) % 8 == 0)
                {
                    builder.Append(' ');
                    charBuilder.Append(' ');
                }
            }

            // Different than charBuffer.Length since charBuffer contains an extra " " after the 8th byte.
            var numBytesInLastLine = buffer.Length % 16;

            if (numBytesInLastLine > 0)
            {
                // 2 (between hex and char blocks) + num bytes left (3 per byte)
                var padLength = 2 + (3 * (16 - numBytesInLastLine));
                // extra for space after 8th byte
                if (numBytesInLastLine < 8)
                {
                    padLength++;
                }

                builder.Append(new string(' ', padLength));
                builder.Append(charBuilder);
            }

            _logger.LogBytes(method, buffer.Length, builder.ToString());
        }

        // The below APM methods call the underlying Read/WriteAsync methods which will still be logged.
        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return ReadAsync(buffer, offset, count, default(CancellationToken)).ToApm(callback, state);
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            return asyncResult.ToApmEnd<int>();
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return WriteAsync(buffer, offset, count, default(CancellationToken)).ToApm(callback, state);
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            asyncResult.ToApmEnd();
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            _inner.Dispose();
        }
    }
}
