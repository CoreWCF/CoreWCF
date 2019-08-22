using System;
using System.IO;
using System.IO.Pipelines;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace CoreWCF.Channels.Framing
{
    internal class StreamDuplexPipe : IDuplexPipe
    {
        private static readonly int MinAllocBufferSize = 4096;

        private readonly IDuplexPipe _transport;
        private readonly Stream _stream;

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

            BackgroundTask = RunAsync(stream);
        }

        public Pipe Input { get; }

        public Pipe Output { get; }

        PipeReader IDuplexPipe.Input => Input.Reader;

        PipeWriter IDuplexPipe.Output => Output.Writer;

        public Task BackgroundTask { get; }

        private async Task RunAsync(Stream stream)
        {
            await Task.Yield();
            var inputTask = ReadInputAsync(stream);
            var outputTask = WriteOutputAsync(stream);

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
                    var result = await Output.Reader.ReadAsync();
                    var buffer = result.Buffer;

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
                            if (!MemoryMarshal.TryGetArray(buffer.First, out var segment))
                            {
                                throw new InvalidOperationException("Buffer backed by array was expected");
                            }

                            await stream.WriteAsync(segment.Array, segment.Offset, segment.Count);

                            // Once we've moved past netstandard2.0, we can replace the code with this line:
                            // await stream.WriteAsync(buffer.First);
                        }
                        else
                        {
                            foreach (var memory in buffer)
                            {
                                if (!MemoryMarshal.TryGetArray(memory, out var segment))
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

                    var outputBuffer = Input.Writer.GetMemory(MinAllocBufferSize);

                    if (!MemoryMarshal.TryGetArray((ReadOnlyMemory<byte>)outputBuffer, out var segment))
                    {
                        throw new InvalidOperationException("Buffer backed by array was expected");
                    }
                    var bytesRead = await stream.ReadAsync(segment.Array, segment.Offset, segment.Count);

                    // Once we've moved past netstandard2.0, we can replace the code with this line:
                    // var bytesRead = await stream.ReadAsync(outputBuffer);

                    Input.Writer.Advance(bytesRead);

                    if (bytesRead == 0)
                    {
                        // FIN
                        break;
                    }

                    var result = await Input.Writer.FlushAsync();

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
    }
}
