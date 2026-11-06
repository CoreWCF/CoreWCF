// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Text;
using System.Xml;
using CoreWCF.Channels;
using Xunit;

namespace CoreWCF.Primitives.Tests
{
    public class MtomMessageEncodingBindingElementTests
    {
        private const string Boundary = "corewcf-boundary";

        [Fact]
        public void MaxMimeParts_DefaultsTo1000_AndIsCloned()
        {
            MtomMessageEncodingBindingElement element = new MtomMessageEncodingBindingElement();

            Assert.Equal(1000, element.MaxMimeParts);

            element.MaxMimeParts = 2000;
            MtomMessageEncodingBindingElement clone = (MtomMessageEncodingBindingElement)element.Clone();

            Assert.Equal(2000, clone.MaxMimeParts);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void MaxMimeParts_RequiresPositiveValue(int maxMimeParts)
        {
            MtomMessageEncodingBindingElement element = new MtomMessageEncodingBindingElement();

            Assert.Throws<ArgumentOutOfRangeException>(() => element.MaxMimeParts = maxMimeParts);
        }

        [Fact]
        public void ReadMessage_EnforcesMaxMimeParts()
        {
            byte[] messageBytes = CreateMtomMessage();
            MtomMessageEncodingBindingElement element = new MtomMessageEncodingBindingElement
            {
                MaxMimeParts = 1
            };
            MessageEncoder encoder = element.CreateMessageEncoderFactory().Encoder;
            BufferManager bufferManager = BufferManager.CreateBufferManager(messageBytes.Length, messageBytes.Length);
            Assert.Throws<XmlException>(() => encoder.ReadMessage(
                new ArraySegment<byte>(messageBytes),
                bufferManager,
                GetContentType()));
        }

        [Fact]
        public void ReadStreamedMessage_EnforcesMaxMimeParts()
        {
            byte[] messageBytes = CreateMtomMessage();
            MtomMessageEncodingBindingElement element = new MtomMessageEncodingBindingElement
            {
                MaxMimeParts = 1
            };
            MessageEncoder encoder = element.CreateMessageEncoderFactory().Encoder;

            using (MemoryStream inputStream = new MemoryStream(messageBytes))
            {
                Assert.Throws<XmlException>(() =>
                {
                    using (Message message = encoder.ReadMessageAsync(inputStream, messageBytes.Length, GetContentType()).GetAwaiter().GetResult())
                    using (XmlDictionaryReader reader = message.GetReaderAtBodyContents())
                    {
                        while (reader.Read())
                        {
                        }
                    }
                });
            }
        }

        private static byte[] CreateMtomMessage()
        {
            string messageContent = string.Concat(
                "--", Boundary, "\r\n",
                "Content-Type: application/xop+xml; charset=utf-8; type=\"application/soap+xml\"\r\n",
                "Content-Transfer-Encoding: 8bit\r\n",
                "Content-ID: <rootpart@corewcf>\r\n\r\n",
                "<s:Envelope xmlns:s=\"http://www.w3.org/2003/05/soap-envelope\" xmlns:xop=\"http://www.w3.org/2004/08/xop/include\"><s:Header/><s:Body><xop:Include href=\"cid:part1@corewcf\"/><xop:Include href=\"cid:part2@corewcf\"/></s:Body></s:Envelope>\r\n",
                "--", Boundary, "\r\n",
                "Content-Type: application/octet-stream\r\n",
                "Content-Transfer-Encoding: binary\r\n",
                "Content-ID: <part1@corewcf>\r\n\r\n",
                "part1\r\n",
                "--", Boundary, "\r\n",
                "Content-Type: application/octet-stream\r\n",
                "Content-Transfer-Encoding: binary\r\n",
                "Content-ID: <part2@corewcf>\r\n\r\n",
                "part2\r\n",
                "--", Boundary, "--\r\n");
            return Encoding.UTF8.GetBytes(messageContent);
        }

        private static string GetContentType() =>
            $"multipart/related; type=\"application/xop+xml\"; start=\"<rootpart@corewcf>\"; boundary=\"{Boundary}\"; start-info=\"application/soap+xml\"";
    }
}
