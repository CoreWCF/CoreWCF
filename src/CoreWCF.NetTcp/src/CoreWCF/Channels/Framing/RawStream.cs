using CoreWCF.Runtime;
using System;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.IO.Pipelines;
using System.Threading;
using System.Threading.Tasks;

namespace CoreWCF.Channels.Framing
{
    public class RawStream : Stream
    {
        private readonly PipeReader _input;
        private readonly PipeWriter _output;
        private bool _canRead;
        private object _thisLock;
        private TaskCompletionSource<object> _unwrapTcs;

        public RawStream(PipeReader input, PipeWriter output)
        {
            _input = input;
            _output = output;
            _canRead = true;
            _thisLock = new object();
        }

        public override bool CanRead
        {
            get
            {
                lock (_thisLock)
                {
                    return _canRead;
                }
            }
        }

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length
        {
            get
            {
                throw new NotSupportedException();
            }
        }

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
            // ValueTask uses .GetAwaiter().GetResult() if necessary
            // https://github.com/dotnet/corefx/blob/f9da3b4af08214764a51b2331f3595ffaf162abe/src/System.Threading.Tasks.Extensions/src/System/Threading/Tasks/ValueTask.cs#L156
            return ReadAsyncInternal(new Memory<byte>(buffer, offset, count)).Result;
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return ReadAsyncInternal(new Memory<byte>(buffer, offset, count)).AsTask();
        }

        // TODO: Uncomment once moved to netstandard2.1+
        // public override ValueTask<int> ReadAsync(Memory<byte> destination, CancellationToken cancellationToken = default)
        // {
        //     return ReadAsyncInternal(destination);
        // }

        public override void Write(byte[] buffer, int offset, int count)
        {
            WriteAsync(buffer, offset, count).GetAwaiter().GetResult();
        }

        public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (buffer != null)
            {
                _output.Write(new ReadOnlySpan<byte>(buffer, offset, count));
            }

            await _output.FlushAsync(cancellationToken);
        }

        // TODO: Uncomment once moved to netstandard2.1+
        //public override async ValueTask WriteAsync(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default)
        //{
        //    _output.Write(source.Span);
        //    await _output.FlushAsync(cancellationToken);
        //}

        public override void Flush()
        {
            FlushAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return WriteAsync(null, 0, 0, cancellationToken);
        }

        private async ValueTask<int> ReadAsyncInternal(Memory<byte> destination)
        {
            while (true)
            {
                if (!CanRead)
                {
                    await _unwrapTcs.Task;
                    return 0;
                }

                var result = await _input.ReadAsync();
                if (!CanRead)
                {
                    _input.AdvanceTo(result.Buffer.Start);
                    await _unwrapTcs.Task;
                    return 0;
                }

                var readableBuffer = result.Buffer;
                try
                {
                    if (!readableBuffer.IsEmpty)
                    {
                        // buffer.Count is int
                        var count = (int)Math.Min(readableBuffer.Length, destination.Length);
                        readableBuffer = readableBuffer.Slice(0, count);
                        readableBuffer.CopyTo(destination.Span);
                        return count;
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
            var task = ReadAsync(buffer, offset, count, default(CancellationToken), state);
            if (callback != null)
            {
                task.ContinueWith(t => callback.Invoke(t));
            }
            return task;
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            return ((Task<int>)asyncResult).GetAwaiter().GetResult();
        }

        private Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken, object state)
        {
            var tcs = new TaskCompletionSource<int>(state);
            var task = ReadAsync(buffer, offset, count, cancellationToken);
            task.ContinueWith((task2, state2) =>
            {
                var tcs2 = (TaskCompletionSource<int>)state2;
                if (task2.IsCanceled)
                {
                    tcs2.SetCanceled();
                }
                else if (task2.IsFaulted)
                {
                    tcs2.SetException(task2.Exception);
                }
                else
                {
                    tcs2.SetResult(task2.Result);
                }
            }, tcs, cancellationToken);
            return tcs.Task;
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            var task = WriteAsync(buffer, offset, count, default(CancellationToken), state);
            if (callback != null)
            {
                task.ContinueWith(t => callback.Invoke(t));
            }
            return task;
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            ((Task<object>)asyncResult).GetAwaiter().GetResult();
        }

        private Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken, object state)
        {
            var tcs = new TaskCompletionSource<object>(state);
            var task = WriteAsync(buffer, offset, count, cancellationToken);
            task.ContinueWith((task2, state2) =>
            {
                var tcs2 = (TaskCompletionSource<object>)state2;
                if (task2.IsCanceled)
                {
                    tcs2.SetCanceled();
                }
                else if (task2.IsFaulted)
                {
                    tcs2.SetException(task2.Exception);
                }
                else
                {
                    tcs2.SetResult(null);
                }
            }, tcs, cancellationToken);
            return tcs.Task;
        }

        public void StartUnwrapRead()
        {
            // Upon sending the session end byte, the client can start another session immediately.
            // We need to stop those bytes from being consumed by the upgrade stream (e.g. NegotiateStream),
            // but we can't return a zero byte response to the pending read until after we've sent the session 
            // end byte otherwise it will close the upgrade stream and prevent the session end byte from being
            // sent. Calling StartUnwrapRead prevents any reads from completing until FinisheUnwrapRead has
            // been called. This ensures any client bytes from the next session are not consumed and still
            // allows a write to be sent through the wrapping stream.
            lock (_thisLock)
            {
                _unwrapTcs = new TaskCompletionSource<object>();
                _canRead = false;
            }

            _input.CancelPendingRead();
        }

        public void FinishUnwrapRead()
        {
            Fx.Assert(_unwrapTcs != null, "StartUnwrapRead must be called first");
            _unwrapTcs.TrySetResult(null);
        }
    }
}
