// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO.Pipelines;
using System.Threading.Tasks;
using CoreWCF.Channels;
using Xunit;

namespace CoreWCF.Primitives.Tests
{
    // MaxMessageSizePipeReader is internal, so it is reached with minimal reflection rather than by
    // adding InternalsVisibleTo, as elsewhere in this repo.
    public class MaxMessageSizePipeReaderTests
    {
        [Fact]
        public async Task MessageUnderTheLimitIsReadThrough()
        {
            Pipe pipe = new();
            PipeReader reader = CreateReader(pipe.Reader, maxMessageSize: 50);

            await pipe.Writer.WriteAsync(new byte[40]);

            ReadResult result = await reader.ReadAsync();

            Assert.Equal(40, result.Buffer.Length);
        }

        [Fact]
        public async Task MessageBufferedPastTheLimitFaults()
        {
            // How a buffered read behaves: nothing is consumed until the whole message has arrived,
            // so the limit is reached by the buffer growing.
            Pipe pipe = new();
            PipeReader reader = CreateReader(pipe.Reader, maxMessageSize: 50);

            await pipe.Writer.WriteAsync(new byte[100]);

            await Assert.ThrowsAsync<CommunicationException>(async () => await reader.ReadAsync());
        }

        [Fact]
        public async Task MessageConsumedInPiecesPastTheLimitFaults()
        {
            // How a streamed read behaves: the buffer stays small because the reader consumes as it
            // goes, so only the running total gives the message's real size away.
            Pipe pipe = new();
            PipeReader reader = CreateReader(pipe.Reader, maxMessageSize: 50);

            await pipe.Writer.WriteAsync(new byte[30]);
            ReadResult first = await reader.ReadAsync();
            reader.AdvanceTo(first.Buffer.End);

            await pipe.Writer.WriteAsync(new byte[30]);

            // Only 30 bytes are buffered, but 60 have now come through.
            await Assert.ThrowsAsync<CommunicationException>(async () => await reader.ReadAsync());
        }

        private static PipeReader CreateReader(PipeReader inner, long maxMessageSize)
        {
            Type readerType = typeof(MessageEncoder).Assembly
                .GetType("CoreWCF.Channels.MaxMessageSizePipeReader", throwOnError: true);

            return (PipeReader)Activator.CreateInstance(readerType, new object[] { inner, maxMessageSize });
        }
    }
}
