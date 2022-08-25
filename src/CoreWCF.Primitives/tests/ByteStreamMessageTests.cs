// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.Channels;
using Helpers;
using Xunit;

namespace CoreWCF.Primitives.Tests
{
    public class ByteStreamMessageTests
    {
        [Fact]
        public void TestCreatingByteStreamMessageWithZeroOffsetByteArray()
        {
            ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[10]);
            buffer.FillWithData();

            Message message = ByteStreamMessage.CreateMessage(buffer);
            byte[] actualBytes = message.GetBody<byte[]>();
            Assert.Equal(buffer, new ArraySegment<byte>(actualBytes));
        }

        [Fact]
        public void TestCreatingByteStreamMessageWithByteArrayThenCreateCopy()
        {
            string messageContents = "This is a text message";
            byte[] bytes = Encoding.ASCII.GetBytes(messageContents);
            ArraySegment<byte> buffer = new ArraySegment<byte>(bytes);

            Message message = ByteStreamMessage.CreateMessage(buffer);
            MessageBuffer copy = message.CreateBufferedCopy(int.MaxValue);
            message.Close();
            Message message2 = copy.CreateMessage();

            using (StreamReader reader = new StreamReader(message2.GetBody<Stream>()))
            {
                string copyOfMessageAsString = reader.ReadToEnd();
                Assert.Equal(messageContents, copyOfMessageAsString);
            }

            copy.CreateMessage().Close();//verify the refcount is OK

            copy.Close();
        }

        [Fact]
        public void TestCreatingByteStreamMessageWithNonZeroOffsetByteArray()
        {
            ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[20], 2, 18);
            buffer.FillWithData();

            Message message = ByteStreamMessage.CreateMessage(buffer);
            byte[] actualBytes = message.GetBody<byte[]>();
            Assert.Equal(buffer, new ArraySegment<byte>(actualBytes));
        }

        [Fact]
        public void TestCreatingByteStreamMessageWithByteArrayAndBufferManager()
        {
            const int bufferSize = 20;
            BufferManager bufferManager = BufferManager.CreateBufferManager(100000, 100000);

            ArraySegment<byte> buffer = new ArraySegment<byte>(bufferManager.TakeBuffer(bufferSize));
            buffer.FillWithData();

            Message message = ByteStreamMessage.CreateMessage(buffer, bufferManager);
            byte[] actualBytes = message.GetBody<byte[]>();//calling GetBody will close the internal reader and return the buffer to the pool
            Assert.Equal(buffer, new ArraySegment<byte>(actualBytes));
        }

        [Fact]
        public void TestCreatingByteStreamMessageWithStream()
        {
            MemoryStream ms = new MemoryStream();
            StreamWriter sw = new StreamWriter(ms, Encoding.Default);
            string testContent = "Hello world.";
            sw.Write(testContent);
            sw.Flush();
            ms.Position = 0;

            Message message = ByteStreamMessage.CreateMessage(ms);
            Stream resultStream = message.GetBody<Stream>();
            StreamReader sr = new StreamReader(resultStream, Encoding.Default);
            string resultContent = sr.ReadToEnd();
            Assert.Equal(testContent, resultContent);
        }

        [Fact]
        public void TestCreatingByteStreamMessageWithStreamThenCreateCopy()
        {
            MemoryStream ms = new MemoryStream();
            StreamWriter sw = new StreamWriter(ms, Encoding.Default);
            string testContent = "Hello world.";
            sw.Write(testContent);
            sw.Flush();
            ms.Position = 0;

            Message message = ByteStreamMessage.CreateMessage(ms);

            MessageBuffer copy = message.CreateBufferedCopy(int.MaxValue);
            message.Close();
            Message message2 = copy.CreateMessage();
            Stream resultStream = message2.GetBody<Stream>();
            StreamReader sr = new StreamReader(resultStream, Encoding.Default);
            string resultContent = sr.ReadToEnd();
            Assert.Equal(testContent, resultContent);

            copy.CreateMessage().Close();//verify the refcount is OK

            copy.Close();
        }

        [Fact]
        public void TestCreatingByteStreamMessageWithNullStream()
        {
            Stream stream = null;
            Assert.Throws<ArgumentNullException>(() => ByteStreamMessage.CreateMessage(stream));
        }

        [Fact]
        public void TestCreatingByteStreamMessageWithNullBuffer()
        {
            ArraySegment<byte> buffer = default(ArraySegment<byte>);
            Assert.Throws<ArgumentNullException>(() => ByteStreamMessage.CreateMessage(buffer));
        }

        [Fact]
        public void TestGetStringFromByteStreamMessage()
        {
            byte[] bytes = new byte[] { 1, 2, 3 };
            Message message = ByteStreamMessage.CreateMessage(new ArraySegment<byte>(bytes));
            Assert.Throws<NotSupportedException>(() => message.GetBody<string>());
        }

        [Fact]
        public void TestConsumeByteStreamMessageTwice()
        {
            byte[] bytes = new byte[] { 1, 2, 3 };
            Message message = ByteStreamMessage.CreateMessage(new ArraySegment<byte>(bytes));
            byte[] result = message.GetBody<byte[]>();
            Assert.Throws<InvalidOperationException>(() => result = message.GetBody<byte[]>());
        }

        [Fact]
        public void TestGetByteArrayFromStreamBasedMessage()
        {
            byte[] bytes = new byte[] { 1, 2, 3 };
            MemoryStream ms = new MemoryStream(bytes);
            Message message = ByteStreamMessage.CreateMessage(ms);
            Assert.Throws<InvalidOperationException>(() => message.GetBody<byte[]>());
        }

        [Fact]
        public void TestBodyReaderForMessageCreatedFromStream()
        {
            byte[] bytes = Encoding.ASCII.GetBytes("This is a text message");
            string encodedMessage = Convert.ToBase64String(bytes);

            // The ByteStreamMessage.CreateMessage(..) methods should produce Messages that return a body reader positioned on content. Because
            // we want ByteStreamMessage to be consistent as much as possible with all the other implementations of Message (including the Message
            // base class itself), who provide body readers positioned on content.
            using (MemoryStream stream = new MemoryStream(bytes))
            {
                TestGetReaderAtBodyContents(ByteStreamMessage.CreateMessage(stream), encodedMessage, true);
            }
        }

        [Fact]
        public void TestBodyReaderForMessageCreatedFromArraySegment()
        {
            byte[] bytes = Encoding.ASCII.GetBytes("This is a text message");
            string encodedMessage = Convert.ToBase64String(bytes);

            // The ByteStreamMessage.CreateMessage(..) methods should produce Messages that return a body reader positioned on content. Because
            // we want ByteStreamMessage to be consistent as much as possible with all the other implementations of Message (including the Message
            // base class itself), who provide body readers positioned on content.
            TestGetReaderAtBodyContents(ByteStreamMessage.CreateMessage(new ArraySegment<byte>(bytes)), encodedMessage, true);
        }

        [Fact]
        public void TestBodyReaderForMessageCreatedFromArraySegmentAndBufferManager()
        {
            byte[] bytes = Encoding.ASCII.GetBytes("This is a text message");
            string encodedMessage = Convert.ToBase64String(bytes);
            BufferManager bufferManager = BufferManager.CreateBufferManager(10, 10);

            // The ByteStreamMessage.CreateMessage(..) methods should produce Messages that return a body reader positioned on content. Because
            // we want ByteStreamMessage to be consistent as much as possible with all the other implementations of Message (including the Message
            // base class itself), who provide body readers positioned on content.
            TestGetReaderAtBodyContents(ByteStreamMessage.CreateMessage(new ArraySegment<byte>(bytes), bufferManager), encodedMessage, true);
        }

        [Fact]
        public async Task TestBodyReaderForMessageCreatedWithByteStreamEncoderFromStream()
        {
            byte[] bytes = Encoding.ASCII.GetBytes("This is a text message");
            string encodedMessage = Convert.ToBase64String(bytes);
            MessageEncoder byteStreamEncoder = new ByteStreamMessageEncodingBindingElement().CreateMessageEncoderFactory().Encoder;

            // The byte stream encoder should produce Messages that return a body reader not positioned on content.
            // Because it does so in .net 4.0 and it should continue to do the same in .net 4.5 (and later).
            using (MemoryStream stream = new MemoryStream(bytes))
            {
                TestGetReaderAtBodyContents(await byteStreamEncoder.ReadMessageAsync(stream, 10), encodedMessage, false);
            }
        }

        [Fact]
        public void TestBodyReaderForMessageCreatedWithByteStreamEncoderFromArraySegment()
        {
            byte[] bytes = Encoding.ASCII.GetBytes("This is a text message");
            string encodedMessage = Convert.ToBase64String(bytes);
            BufferManager bufferManager = BufferManager.CreateBufferManager(10, 10);
            MessageEncoder byteStreamEncoder = new ByteStreamMessageEncodingBindingElement().CreateMessageEncoderFactory().Encoder;

            // The byte stream encoder should produce Messages that return a body reader not positioned on content.
            // Because it does so in .net 4.0 and it should continue to do the same in .net 4.5 (and later).
            TestGetReaderAtBodyContents(byteStreamEncoder.ReadMessage(new ArraySegment<byte>(bytes), bufferManager), encodedMessage, false);
        }

        private static void TestGetReaderAtBodyContents(Message message, string expectedValue, bool readerShouldBePositionedOnContent)
        {
            // The first call to GetReaderAtBodyContents() should return a usable reader.
            // The xml should be something like this:
            // <Binary>VGhpcyBpcyBhIHRleHQgbWVzc2FnZQ==</Binary>
            XmlDictionaryReader reader = message.GetReaderAtBodyContents();
            Assert.NotNull(reader);

            if (readerShouldBePositionedOnContent)
            {
                // The reader should be positioned on the <Binary> node.
                Assert.Equal<XmlNodeType>(XmlNodeType.Element, reader.NodeType); // GetReaderAtBodyContents() should return an XmlDictionaryReader positioned on content.
            }
            else
            {
                // The reader should be positioned on None, just before the <Binary> node.
                Assert.Equal<XmlNodeType>(XmlNodeType.None, reader.NodeType); // GetReaderAtBodyContents() should return an XmlDictionaryReader positioned on None.

                // Read the None node, advancing to the <Binary> element.
                Assert.True(reader.Read(), "Read() failed.");

                // The reader should be positioned on the <Binary> node.
                Assert.Equal<XmlNodeType>(XmlNodeType.Element, reader.NodeType);
            }

            // Read the <Binary> node, advancing to the Text node.
            Assert.True(reader.Read(), "Read() failed.");
            Assert.Equal<XmlNodeType>(XmlNodeType.Text, reader.NodeType);
            Assert.Equal(expectedValue, reader.Value);

            // Read the Text node, advancing to the EndElement node.
            Assert.True(reader.Read(), "Read() failed.");
            Assert.Equal<XmlNodeType>(XmlNodeType.EndElement, reader.NodeType);

            // There should be no more nodes to read.
            for (int i = 0; i < 2; i++) // multiple attempts, to make sure the reader stays finished
            {
                Assert.False(reader.Read(), "Read() should have failed, there should be no more nodes to read.");
            }

            // Subsequent calls to GetReaderAtBodyContents() should fail.
            for (int i = 0; i < 2; i++)
            {
                Assert.Throws< InvalidOperationException>(() => message.GetReaderAtBodyContents());
            }
        }
    }
}
