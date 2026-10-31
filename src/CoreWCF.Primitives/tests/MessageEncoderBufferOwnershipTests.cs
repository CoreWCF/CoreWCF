// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.Channels;
using Xunit;

namespace CoreWCF.Primitives.Tests
{
    // A transport rents the buffer it hands to ReadMessageAsync and drops its own reference right
    // afterwards, so the message it produced is what gives the array back to the pool. Without that
    // the pool bleeds one array per message.
    public class MessageEncoderBufferOwnershipTests
    {
        private const string Soap11Message =
            @"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/""><soap:Body><Ping xmlns=""http://tempuri.org/"" /></soap:Body></soap:Envelope>";

        [Fact]
        public async Task ClosingTheMessage_ReturnsTheBufferToTheBufferManager()
        {
            byte[] messageBytes = Encoding.UTF8.GetBytes(Soap11Message);
            BufferManager bufferManager = BufferManager.CreateBufferManager(int.MaxValue, int.MaxValue);

            byte[] rented = bufferManager.TakeBuffer(messageBytes.Length);
            messageBytes.CopyTo(rented, 0);

            MessageEncoder encoder = new TextMessageEncodingBindingElement { MessageVersion = MessageVersion.Soap11 }
                .CreateMessageEncoderFactory()
                .Encoder;

            Message message = await encoder.ReadMessageAsync(
                new ReadOnlySequence<byte>(rented, 0, messageBytes.Length), bufferManager, "text/xml; charset=utf-8");
            message.Close();

            // Back in the pool, so the next request for the same size hands out the same array.
            Assert.Same(rented, bufferManager.TakeBuffer(messageBytes.Length));
        }

        [Fact]
        public async Task ClosingTheMessageWhileABodyReaderIsStillOpen_DefersTheReturnUntilTheReaderCloses()
        {
            byte[] messageBytes = Encoding.UTF8.GetBytes(Soap11Message);
            BufferManager bufferManager = BufferManager.CreateBufferManager(int.MaxValue, int.MaxValue);

            byte[] rented = bufferManager.TakeBuffer(messageBytes.Length);
            messageBytes.CopyTo(rented, 0);

            MessageEncoder encoder = new TextMessageEncodingBindingElement { MessageVersion = MessageVersion.Soap11 }
                .CreateMessageEncoderFactory()
                .Encoder;

            Message message = await encoder.ReadMessageAsync(
                new ReadOnlySequence<byte>(rented, 0, messageBytes.Length), bufferManager, "text/xml; charset=utf-8");

            XmlDictionaryReader reader = message.GetReaderAtBodyContents();
            message.Close();
            reader.Close();

            // Closing the message could not release the buffer while the reader was reading out of
            // it, so closing the reader has to be what completes the job.
            Assert.Same(rented, bufferManager.TakeBuffer(messageBytes.Length));
        }

        [Fact]
        public async Task ClosingAByteStreamMessage_ReturnsTheBufferToTheBufferManager()
        {
            byte[] bodyBytes = Encoding.UTF8.GetBytes("a byte stream body");
            BufferManager bufferManager = BufferManager.CreateBufferManager(int.MaxValue, int.MaxValue);

            byte[] rented = bufferManager.TakeBuffer(bodyBytes.Length);
            bodyBytes.CopyTo(rented, 0);

            MessageEncoder encoder = new ByteStreamMessageEncodingBindingElement()
                .CreateMessageEncoderFactory()
                .Encoder;

            Message message = await encoder.ReadMessageAsync(
                new ReadOnlySequence<byte>(rented, 0, bodyBytes.Length), bufferManager, encoder.ContentType);
            message.Close();

            Assert.Same(rented, bufferManager.TakeBuffer(bodyBytes.Length));
        }
    }
}
