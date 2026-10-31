// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Buffers;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using CoreWCF.Channels;
using Xunit;

namespace CoreWCF.Primitives.Tests
{
    // ReadMessage(ArraySegment<byte>, ...) and ReadMessageAsync(ReadOnlySequence<byte>, ...) forward
    // to each other so that an encoder only has to implement one of them. These tests pin down what
    // happens for each combination, in particular that implementing neither fails with a diagnosable
    // exception rather than exhausting the stack.
    public class MessageEncoderOverrideDetectionTests
    {
        private const string SoapMessage =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/""><soap:Body /></soap:Envelope>";

        private static ReadOnlySequence<byte> Buffer => new(Encoding.UTF8.GetBytes(SoapMessage));

        private static BufferManager BufferManager => BufferManager.CreateBufferManager(int.MaxValue, int.MaxValue);

        [Fact]
        public async Task EncoderImplementingNeitherOverload_ThrowsNotImplemented()
        {
            var encoder = new NoOverloadEncoder();

            NotImplementedException exception = await Assert.ThrowsAsync<NotImplementedException>(
                async () => await encoder.ReadMessageAsync(Buffer, BufferManager, encoder.ContentType));

            Assert.Contains(nameof(NoOverloadEncoder), exception.Message);
        }

        [Fact]
        public async Task EncoderImplementingNeitherOverload_ReturnsTheBufferItRentedBeforeThrowing()
        {
            var encoder = new NoOverloadEncoder();
            BufferManager bufferManager = BufferManager.CreateBufferManager(int.MaxValue, int.MaxValue);

            // Seed the pool so the array the bridge rents is one we hold a reference to.
            int length = (int)Buffer.Length;
            byte[] pooled = bufferManager.TakeBuffer(length);
            bufferManager.ReturnBuffer(pooled);

            await Assert.ThrowsAsync<NotImplementedException>(
                async () => await encoder.ReadMessageAsync(Buffer, bufferManager, encoder.ContentType));

            // The bridge copies into a rented buffer before calling ReadMessage, and nothing owns
            // that buffer once the call throws.
            Assert.Same(pooled, bufferManager.TakeBuffer(length));
        }

        [Fact]
        public async Task EncoderImplementingOnlySyncOverload_IsBridgedToAsync()
        {
            var encoder = new SyncOnlyEncoder();

            Message message = await encoder.ReadMessageAsync(Buffer, BufferManager, encoder.ContentType);

            Assert.NotNull(message);
            Assert.True(encoder.SyncOverloadCalled);
        }

        [Fact]
        public void EncoderImplementingOnlyAsyncOverload_IsBridgedToSync()
        {
            var encoder = new AsyncOnlyEncoder();

#pragma warning disable CS0612
            Message message = encoder.ReadMessage(new ArraySegment<byte>(Encoding.UTF8.GetBytes(SoapMessage)), BufferManager, encoder.ContentType);
#pragma warning restore CS0612

            Assert.NotNull(message);
            Assert.True(encoder.AsyncOverloadCalled);
        }

        private abstract class TestEncoder : MessageEncoder
        {
            public override string ContentType => "text/xml; charset=utf-8";

            public override string MediaType => "text/xml";

            public override MessageVersion MessageVersion => MessageVersion.Soap11;

            public override Task<Message> ReadMessageAsync(Stream stream, int maxSizeOfHeaders, string contentType)
                => throw new NotSupportedException();

            public override ArraySegment<byte> WriteMessage(Message message, int maxMessageSize, BufferManager bufferManager, int messageOffset)
                => throw new NotSupportedException();

            public override Task WriteMessageAsync(Message message, Stream stream)
                => throw new NotSupportedException();

            protected static Message CreateMessage() => Message.CreateMessage(MessageVersion.Soap11, "urn:test");
        }

        private sealed class NoOverloadEncoder : TestEncoder
        {
        }

        private sealed class SyncOnlyEncoder : TestEncoder
        {
            public bool SyncOverloadCalled { get; private set; }

            [Obsolete]
            public override Message ReadMessage(ArraySegment<byte> buffer, BufferManager bufferManager, string contentType)
            {
                SyncOverloadCalled = true;
                return CreateMessage();
            }
        }

        private sealed class AsyncOnlyEncoder : TestEncoder
        {
            public bool AsyncOverloadCalled { get; private set; }

            public override ValueTask<Message> ReadMessageAsync(ReadOnlySequence<byte> buffer, BufferManager bufferManager, string contentType)
            {
                AsyncOverloadCalled = true;
                return new ValueTask<Message>(CreateMessage());
            }
        }
    }
}
