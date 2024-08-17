// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.Channels;
using Xunit;

namespace CoreWCF.WebHttp.Tests
{
    public class WebMessageEncoderTests
    {
        [Fact]
        public async Task TestBodyReaderForMessageCreatedWithWebEncoderFromStream()
        {
            byte[] bytes = Encoding.ASCII.GetBytes("This is a text message");
            string encodedMessage = Convert.ToBase64String(bytes);
            MessageEncoder webEncoder = new WebMessageEncodingBindingElement().CreateMessageEncoderFactory().Encoder;

            // The web encoder should produce Messages that return a body reader positioned on content.
            // Because it does so in .net 4.0 and it should continue to do the same in .net 4.5 (and later).
            using (MemoryStream stream = new MemoryStream(bytes))
            {
                TestGetReaderAtBodyContents(await webEncoder.ReadMessageAsync(stream, 10), encodedMessage, true);
            }
        }

        [Fact]
        public void TestBodyReaderForMessageCreatedWithWebEncoderFromArraySegment()
        {
            byte[] bytes = Encoding.ASCII.GetBytes("This is a text message");
            string encodedMessage = Convert.ToBase64String(bytes);
            BufferManager bufferManager = BufferManager.CreateBufferManager(10, 10);
            MessageEncoder webEncoder = new WebMessageEncodingBindingElement().CreateMessageEncoderFactory().Encoder;

            // The web encoder should produce Messages that return a body reader positioned on content.
            // Because it does so in .net 4.0 and it should continue to do the same in .net 4.5 (and later).
            TestGetReaderAtBodyContents(webEncoder.ReadMessage(new ArraySegment<byte>(bytes), bufferManager), encodedMessage, true);
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
                Assert.Throws<InvalidOperationException>(() => message.GetReaderAtBodyContents());
            }
        }
    }
}
