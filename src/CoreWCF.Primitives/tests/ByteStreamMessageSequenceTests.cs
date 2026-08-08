// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.Channels;
using Xunit;

namespace CoreWCF.Primitives.Tests
{
    // The ByteStream encoder reads from a ReadOnlySequence<byte>, which - unlike the ArraySegment it
    // replaced - can span several segments and can start part way into the first one. These tests
    // cover the shapes a PipeReader actually produces, which a sequence built from a single array at
    // offset zero never exercises.
    public class ByteStreamMessageSequenceTests
    {
        private const string ContentType = "application/octet-stream";

        private static MessageEncoder Encoder
            => new ByteStreamMessageEncodingBindingElement().CreateMessageEncoderFactory().Encoder;

        private static BufferManager BufferManager
            => BufferManager.CreateBufferManager(int.MaxValue, int.MaxValue);

        private static byte[] Payload(int length)
            => Enumerable.Range(0, length).Select(i => (byte)(i % 251)).ToArray();

        // The encoder takes ownership of a single segment buffer and hands it back to the manager
        // when the message closes, so it has to have come from that manager in the first place.
        private static ReadOnlySequence<byte> Rent(BufferManager bufferManager, byte[] payload, int offset = 0)
        {
            byte[] rented = bufferManager.TakeBuffer(offset + payload.Length);
            payload.CopyTo(rented, offset);
            return new ReadOnlySequence<byte>(rented, offset, payload.Length);
        }

        [Fact]
        public async Task MultiSegmentSequence_GetBodyReturnsEveryByte()
        {
            byte[] expected = Payload(300);
            BufferManager bufferManager = BufferManager;
            ReadOnlySequence<byte> sequence = CreateMultiSegment(bufferManager, expected, segmentSize: 64);
            Assert.False(sequence.IsSingleSegment);

            Message message = await Encoder.ReadMessageAsync(sequence, bufferManager, ContentType);

            Assert.Equal(expected, message.GetBody<byte[]>());
        }

        [Fact]
        public async Task MultiSegmentSequence_ReadInChunks_ReturnsEachByteOnce()
        {
            byte[] expected = Payload(300);
            BufferManager bufferManager = BufferManager;
            ReadOnlySequence<byte> sequence = CreateMultiSegment(bufferManager, expected, segmentSize: 64);
            Assert.False(sequence.IsSingleSegment);

            Message message = await Encoder.ReadMessageAsync(sequence, bufferManager, ContentType);

            Assert.Equal(expected, ReadBodyInChunks(message, chunkSize: 37));
        }

        [Fact]
        public async Task SingleSegmentSequence_ReadInChunks_ReturnsEachByteOnce()
        {
            byte[] expected = Payload(300);
            BufferManager bufferManager = BufferManager;

            Message message = await Encoder.ReadMessageAsync(Rent(bufferManager, expected), bufferManager, ContentType);

            Assert.Equal(expected, ReadBodyInChunks(message, chunkSize: 37));
        }

        [Fact]
        public async Task SequenceStartingPastTheStartOfItsSegment_ReadsOnlyTheSlice()
        {
            // A sequence whose Start is not at index 0 of its first segment: the reader must treat
            // positions as relative to the sequence, not to the underlying segment.
            const int offset = 91;
            byte[] expected = Payload(209);
            BufferManager bufferManager = BufferManager;

            Message message = await Encoder.ReadMessageAsync(
                Rent(bufferManager, expected, offset), bufferManager, ContentType);

            Assert.Equal(expected, ReadBodyInChunks(message, chunkSize: 37));
        }

        [Fact]
        public async Task EmptySequenceIsRejected()
        {
            await Assert.ThrowsAsync<ArgumentException>(
                () => Encoder.ReadMessageAsync(ReadOnlySequence<byte>.Empty, BufferManager, ContentType).AsTask());
        }

        [Fact]
        public async Task BodyStreamOverASingleSegment_IsStillSeekable()
        {
            // GetBody<Stream>() has always handed back a seekable stream over the message buffer,
            // and services rely on Length and Position being usable.
            byte[] expected = Payload(300);
            BufferManager bufferManager = BufferManager;

            Message message = await Encoder.ReadMessageAsync(Rent(bufferManager, expected), bufferManager, ContentType);

            using Stream body = message.GetBody<Stream>();

            Assert.True(body.CanSeek);
            Assert.Equal(expected.Length, body.Length);
        }

        [Fact]
        public async Task WriteBodyContents_ToAWriterOtherThanXmlByteStreamWriter_WritesTheBody()
        {
            byte[] expected = Encoding.ASCII.GetBytes("This is a text message");
            BufferManager bufferManager = BufferManager;
            ReadOnlySequence<byte> sequence = CreateMultiSegment(bufferManager, expected, segmentSize: 8);

            Message message = await Encoder.ReadMessageAsync(sequence, bufferManager, ContentType);

            using var stream = new MemoryStream();
            using (XmlDictionaryWriter writer = XmlDictionaryWriter.CreateTextWriter(stream, Encoding.UTF8, ownsStream: false))
            {
                message.WriteBodyContents(writer);
            }

            string written = Encoding.UTF8.GetString(stream.ToArray());
            Assert.Contains(Convert.ToBase64String(expected), written);
        }

        private static byte[] ReadBodyInChunks(Message message, int chunkSize)
        {
            using XmlDictionaryReader reader = message.GetReaderAtBodyContents();

            // The encoder hands back a reader positioned before the <Binary> element.
            while (reader.NodeType != XmlNodeType.Element)
            {
                Assert.True(reader.Read(), "Failed to reach the body element.");
            }

            Assert.True(reader.Read(), "Failed to reach the body content.");

            var actual = new List<byte>();
            byte[] chunk = new byte[chunkSize];
            int read;
            while ((read = reader.ReadContentAsBase64(chunk, 0, chunk.Length)) > 0)
            {
                actual.AddRange(chunk.Take(read));
            }

            return actual.ToArray();
        }

        // Every segment is rented too: handing the encoder a BufferManager transfers ownership of
        // each buffer the sequence is built from, not just the first.
        private static ReadOnlySequence<byte> CreateMultiSegment(BufferManager bufferManager, byte[] data, int segmentSize)
        {
            Segment first = null;
            Segment last = null;

            for (int i = 0; i < data.Length; i += segmentSize)
            {
                int length = Math.Min(segmentSize, data.Length - i);
                byte[] rented = bufferManager.TakeBuffer(length);
                Array.Copy(data, i, rented, 0, length);

                ReadOnlyMemory<byte> memory = new(rented, 0, length);
                last = first is null ? first = new Segment(memory) : last.Append(memory);
            }

            return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
        }

        private sealed class Segment : ReadOnlySequenceSegment<byte>
        {
            public Segment(ReadOnlyMemory<byte> memory) => Memory = memory;

            public Segment Append(ReadOnlyMemory<byte> memory)
            {
                var next = new Segment(memory) { RunningIndex = RunningIndex + Memory.Length };
                Next = next;
                return next;
            }
        }
    }
}
