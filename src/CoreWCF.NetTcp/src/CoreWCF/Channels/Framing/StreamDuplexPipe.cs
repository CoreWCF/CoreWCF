// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using CoreWCF.Runtime;

namespace CoreWCF.Channels.Framing
{
    internal class StreamDuplexPipe : IDuplexPipe
    {
        private static readonly int s_minAllocBufferSize = 4096;

        private readonly IDuplexPipe _transport;
        private readonly Stream _stream;
        private TaskCompletionSource<object> _readTCS; //= new TaskCompletionSource<Object>(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly CustomPipeReader _customePipeReader;
        private AsyncManualResetEvent _asyncResetEvent = new AsyncManualResetEvent();


        // TODO: Have a mechanism to stop wrapping as Net.Tcp allows you to unwrap a connection and make it raw again
        public StreamDuplexPipe(IDuplexPipe transport, Stream stream)
        {
            _transport = transport;
            _stream = stream;
            Input = new Pipe(new PipeOptions
            (
                readerScheduler: PipeScheduler.Inline,
                writerScheduler: PipeScheduler.Inline,
                useSynchronizationContext: false
            ));
            Output = new Pipe(new PipeOptions
            (
                readerScheduler: PipeScheduler.Inline,
                writerScheduler: PipeScheduler.Inline,
                useSynchronizationContext: false
            ));
            _customePipeReader = new CustomPipeReader(Input.Reader, this);
            BackgroundTask = RunAsync(stream);
        }

        public Pipe Input { get; }

        public Pipe Output { get; }

        

        PipeReader IDuplexPipe.Input => _customePipeReader;

        PipeWriter IDuplexPipe.Output => Output.Writer;

        public Task BackgroundTask { get; }

        private async Task RunAsync(Stream stream)
        {
            await Task.Yield();
            Task inputTask = ReadInputAsync(stream);
            Task outputTask = WriteOutputAsync(stream);

            await inputTask;
            await outputTask;
        }

        private async Task WriteOutputAsync(Stream stream)
        {
            try
            {
                if (stream == null)
                {
                    return;
                }

                while (true)
                {
                    ReadResult result = await Output.Reader.ReadAsync();
                    System.Buffers.ReadOnlySequence<byte> buffer = result.Buffer;

                    try
                    {
                        if (buffer.IsEmpty)
                        {
                            if (result.IsCompleted)
                            {
                                _stream.Dispose();
                                break;
                            }
                            await stream.FlushAsync();
                        }
                        else if (buffer.IsSingleSegment)
                        {
                            if (!MemoryMarshal.TryGetArray(buffer.First, out ArraySegment<byte> segment))
                            {
                                throw new InvalidOperationException("Buffer backed by array was expected");
                            }

                            await stream.WriteAsync(segment.Array, segment.Offset, segment.Count);

                            // Once we've moved past netstandard2.0, we can replace the code with this line:
                            // await stream.WriteAsync(buffer.First);
                        }
                        else
                        {
                            foreach (ReadOnlyMemory<byte> memory in buffer)
                            {
                                if (!MemoryMarshal.TryGetArray(memory, out ArraySegment<byte> segment))
                                {
                                    throw new InvalidOperationException("Buffer backed by array was expected");
                                }

                                await stream.WriteAsync(segment.Array, segment.Offset, segment.Count);

                                // Once we've moved past netstandard2.0, we can replace the code with this line:
                                // await stream.WriteAsync(memory);
                            }
                        }
                    }
                    finally
                    {
                        Output.Reader.AdvanceTo(buffer.End);
                    }
                }
            }
            catch (Exception)
            {
                // TODO: make sure the exception propagates somehow
            }
            finally
            {
                Output.Reader.Complete();
            }
        }

        private async Task ReadInputAsync(Stream stream)
        {
            Exception error = null;

            try
            {
                if (stream == null)
                {
                    return;
                }

                while (true)
                {
                    Memory<byte> outputBuffer = Input.Writer.GetMemory(s_minAllocBufferSize);

                    if (!MemoryMarshal.TryGetArray((ReadOnlyMemory<byte>)outputBuffer, out ArraySegment<byte> segment))
                    {
                        throw new InvalidOperationException("Buffer backed by array was expected");
                    }

                    if(!stream.CanRead)
                    {
                        return;
                    }


                    //_readTCS = new TaskCompletionSource<Object>(TaskCreationOptions.RunContinuationsAsynchronously);
                    //await _readTCS.Task;
                    await _asyncResetEvent.WaitAsync();
                    _asyncResetEvent.Reset();
                    int bytesRead = await stream.ReadAsync(segment.Array, segment.Offset, segment.Count);

                    // Once we've moved past netstandard2.0, we can replace the code with this line:
                    // var bytesRead = await stream.ReadAsync(outputBuffer);

                    Input.Writer.Advance(bytesRead);

                    if (bytesRead == 0)
                    {
                        // FIN
                        break;
                    }

                    FlushResult result = await Input.Writer.FlushAsync();

                    if (result.IsCompleted)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                // Don't rethrow the exception. It should be handled by the Pipeline consumer.
                error = ex;
            }
            finally
            {
                Input.Writer.Complete(error);
            }
        }

        class CustomPipeReader : PipeReader
        {
            private PipeReader _inputPipeReader;
            private StreamDuplexPipe _parent;

            public CustomPipeReader(PipeReader inputPipeReader, StreamDuplexPipe parentCall)
            {
                _inputPipeReader = inputPipeReader;
                _parent = parentCall;
              
            }

            public override void AdvanceTo(SequencePosition consumed) => _inputPipeReader.AdvanceTo(consumed);
            public override void AdvanceTo(SequencePosition consumed, SequencePosition examined) => _inputPipeReader.AdvanceTo(consumed, examined);
            public override void CancelPendingRead() => _inputPipeReader.CancelPendingRead();
            public override void Complete(Exception exception = null) => _inputPipeReader.Complete(exception);
            public override void OnWriterCompleted(Action<Exception, object> callback, object state) => _inputPipeReader.OnWriterCompleted(callback, state);
            public override ValueTask<ReadResult> ReadAsync(CancellationToken cancellationToken = default)
            {

                _parent._asyncResetEvent.Set();
                ValueTask<ReadResult> result = _inputPipeReader.ReadAsync(cancellationToken);
                //long length = result.Result.Buffer.Length;
                return result;
            }
            public override bool TryRead(out ReadResult result) => _inputPipeReader.TryRead(out result);

        }
    }
}
