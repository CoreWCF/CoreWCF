// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF;
using CoreWCF.Channels;
using Helpers;
using Xunit;

namespace CoreWCF.Primitives.Tests
{
    public class MessageEncoderTest
    {
        [Fact]
        public async Task BasicTextTest()
        {
            int bad = 0;
            int good = 0;
            try
            {
                InterestingMessageSet ims = new InterestingMessageSet();
                Encoding encoding = Encoding.UTF8;
                MessageEncoder f = (new TextMessageEncodingBindingElement(MessageVersion.Soap12WSAddressing10, encoding)).CreateMessageEncoderFactory().Encoder;

                while (ims.MoveNext())
                {
                    Message myMessage = (Message)ims.Current;
                    var s = new MemoryStream();
                    await f.WriteMessageAsync(myMessage, s);
                    s.Seek(0, SeekOrigin.Begin);
                    _ = new StreamReader(s);
                    s.Seek(0, SeekOrigin.Begin);
                    Message m2 = await f.ReadMessageAsync(s, int.MaxValue);

                    // original got closed by sending, so recreate it:
                    myMessage = ims.CurrentParameters.CreateMessage();
                    if (MessageTestUtilities.AreMessagesEqual(myMessage, m2))
                    {
                        good++;
                    }
                    else
                    {
                        bad++;
                    }
                }

                Assert.False(bad > 0, $"Messages not equal! Failure!({bad} bad ones, {good} good ones.)");
            }
            catch (Exception ex)
            {
                Assert.Fail($"Exception caught: {ex.Message} More information: {bad} bad ones, {good} good ones.");
            }
        }

        [Fact]
        public void BodyWriterMessageTest()
        {
            string action = "http://www.action.com/";
            Message m1 = Message.CreateMessage(MessageVersion.Soap12WSAddressing10, action, new CustomGeneratedBodyWriter(2, 1024));
            Message m1p = Message.CreateMessage(MessageVersion.Soap12WSAddressing10, action, new CustomGeneratedBodyWriter(2, 1024));

            // Note, m1 is closed by this, which is we compare m2 with m1p
            Message m2 = MessageTestUtilities.SendAndReceiveMessage(m1);
            Assert.True(MessageTestUtilities.AreBodiesEqual(m1p, m2, true, true));
        }

        [Fact]
        public void ObjectHeaderTests()
        {
            Message message = Message.CreateMessage(MessageVersion.Soap12WSAddressing10, "http://www.action.com/");
            GeneratedSerializableObject generatedSerializableObject = new GeneratedSerializableObject(2, 200L);
            MessageHeader header = MessageHeader.CreateHeader("foo", "", generatedSerializableObject);
            message.Headers.Add(header);
            Message message2 = MessageTestUtilities.SendAndReceiveMessage(message);
            int num = message2.Headers.FindHeader("foo", "");
            Assert.NotEqual(-1, num);

            object header2 = message2.Headers.GetHeader<GeneratedSerializableObject>(num);
            object header3 = message2.Headers.GetHeader<GeneratedSerializableObject>("foo", "");
            Assert.False(!generatedSerializableObject.Equals(header2) || !generatedSerializableObject.Equals(header3));

            string s = "<s:Envelope xmlns:s=\"http://www.w3.org/2003/05/soap-envelope\" xmlns:a=\"http://schemas.xmlsoap.org/ws/2003/03/addressing\"><s:Header /><s:Body /></s:Envelope>";
            message = Message.CreateMessage(new XmlTextReader(new StringReader(s)), 2147483647, MessageVersion.Default);
            message2 = Message.CreateMessage(new XmlTextReader(new StringReader(s)), 2147483647, MessageVersion.Default);
            Message two = MessageTestUtilities.SendAndReceiveMessage(message);
            Assert.True(MessageTestUtilities.AreBodiesEqual(message2, two));
        }

        [Fact]
        public async Task BufferMessageStreamAsync_GrowsAndTransfersFinalBuffer()
        {
            byte[] expected = CreateBufferTestPayload(10000);
            MessageEncoder testEncoder = new TextMessageEncodingBindingElement(MessageVersion.Soap12WSAddressing10, Encoding.UTF8).CreateMessageEncoderFactory().Encoder;
            TrackingBufferManager bufferManager = new TrackingBufferManager();

            using (MemoryStream stream = new MemoryStream(expected))
            {
                ArraySegment<byte> buffered = await InvokeBufferMessageStreamAsync(testEncoder, stream, bufferManager, 20000);
                byte[] actual = new byte[buffered.Count];
                Buffer.BlockCopy(buffered.Array, buffered.Offset, actual, 0, buffered.Count);

                Assert.Equal(expected, actual);
                Assert.Single(bufferManager.ReturnedBuffers);

                bufferManager.ReturnBuffer(buffered.Array);
                Assert.Equal(2, bufferManager.ReturnedBuffers.Count);
                Assert.Same(buffered.Array, bufferManager.ReturnedBuffers[1]);
            }
        }

        [Fact]
        public async Task BufferMessageStreamAsync_ReturnsBufferOnMaxSizeException()
        {
            byte[] expected = CreateBufferTestPayload(9000);
            MessageEncoder testEncoder = new TextMessageEncodingBindingElement(MessageVersion.Soap12WSAddressing10, Encoding.UTF8).CreateMessageEncoderFactory().Encoder;
            TrackingBufferManager bufferManager = new TrackingBufferManager();

            using (MemoryStream stream = new MemoryStream(expected))
            {
                await Assert.ThrowsAsync<CommunicationException>(() => InvokeBufferMessageStreamAsync(testEncoder, stream, bufferManager, 8192));
            }

            Assert.Single(bufferManager.TakenBuffers);
            Assert.Single(bufferManager.ReturnedBuffers);
            Assert.Same(bufferManager.TakenBuffers[0], bufferManager.ReturnedBuffers[0]);
        }

        [Fact]
        public void ByteStreamWriteMessage_GrowsAndTransfersFinalBuffer()
        {
            byte[] expected = CreateBufferTestPayload(10000);
            MessageEncoder encoder = new ByteStreamMessageEncodingBindingElement().CreateMessageEncoderFactory().Encoder;
            TrackingBufferManager bufferManager = new TrackingBufferManager();
            Message message = ByteStreamMessage.CreateMessage(new ArraySegment<byte>(expected));

            ArraySegment<byte> encoded = encoder.WriteMessage(message, 20000, bufferManager);
            byte[] actual = new byte[encoded.Count];
            Buffer.BlockCopy(encoded.Array, encoded.Offset, actual, 0, encoded.Count);

            Assert.Equal(expected, actual);
            Assert.Single(bufferManager.ReturnedBuffers);

            bufferManager.ReturnBuffer(encoded.Array);
            Assert.Equal(2, bufferManager.ReturnedBuffers.Count);
            Assert.Same(encoded.Array, bufferManager.ReturnedBuffers[1]);
        }

        [Fact]
        public void ByteStreamWriteMessage_ReturnsBufferOnMaxSizeException()
        {
            byte[] expected = CreateBufferTestPayload(9000);
            MessageEncoder encoder = new ByteStreamMessageEncodingBindingElement().CreateMessageEncoderFactory().Encoder;
            TrackingBufferManager bufferManager = new TrackingBufferManager();
            Message message = ByteStreamMessage.CreateMessage(new ArraySegment<byte>(expected));

            Assert.Throws<QuotaExceededException>(() => encoder.WriteMessage(message, 8192, bufferManager));

            Assert.Single(bufferManager.TakenBuffers);
            Assert.Single(bufferManager.ReturnedBuffers);
            Assert.Same(bufferManager.TakenBuffers[0], bufferManager.ReturnedBuffers[0]);
        }

        private static async Task<ArraySegment<byte>> InvokeBufferMessageStreamAsync(MessageEncoder encoder, Stream stream, BufferManager bufferManager, int maxBufferSize)
        {
            MethodInfo method = typeof(MessageEncoder).GetMethod("BufferMessageStreamAsync", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(method);
            Task<ArraySegment<byte>> task = (Task<ArraySegment<byte>>)method.Invoke(encoder, new object[] { stream, bufferManager, maxBufferSize });
            return await task;
        }

        private static byte[] CreateBufferTestPayload(int size)
        {
            byte[] payload = new byte[size];
            for (int i = 0; i < payload.Length; i++)
            {
                payload[i] = (byte)i;
            }

            return payload;
        }

        private sealed class TrackingBufferManager : BufferManager
        {
            public List<byte[]> ReturnedBuffers { get; } = new List<byte[]>();

            public List<byte[]> TakenBuffers { get; } = new List<byte[]>();

            public override void Clear()
            {
            }

            public override void ReturnBuffer(byte[] buffer)
            {
                ReturnedBuffers.Add(buffer);
            }

            public override byte[] TakeBuffer(int bufferSize)
            {
                byte[] buffer = new byte[bufferSize];
                TakenBuffers.Add(buffer);
                return buffer;
            }
        }
    }
}
