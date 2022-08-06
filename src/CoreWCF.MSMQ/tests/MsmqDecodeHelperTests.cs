// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using CoreWCF.Channels;
using CoreWCF.MSMQ.Tests.Helpers;
using Xunit;

namespace CoreWCF.MSMQ.Tests
{
    public class MsmqDecodeHelperTests
    {
        [Fact]
        public void DecodeExampleMessage_Success()
        {
            var bindingElement = new BinaryMessageEncodingBindingElement();
            var encoder = bindingElement.CreateMessageEncoderFactory().Encoder;
            var stream = MessageContainer.GetTestMessage();
            var message = MsmqDecodeHelper.DecodeTransportDatagram(stream, encoder, int.MaxValue);
            Assert.NotNull(message);
            Assert.False(message.IsEmpty);
            Assert.False(message.IsFault);
            Assert.Equal(MessageVersion.Default, message.Version);
            Assert.Equal("http://tempuri.org/ITestContract/Create", message.Headers.Action);
            Assert.Equal(new Uri("net.msmq://localhost/private/wcfQueue"), message.Headers.To);
        }

        [Fact]
        public void DecodeExampleMessage_WhenThrowException()
        {
            var bindingElement = new BinaryMessageEncodingBindingElement();
            var encoder = bindingElement.CreateMessageEncoderFactory().Encoder;
            var stream = MessageContainer.GetTestMessage();
            stream.Seek(2, SeekOrigin.Begin);

            var exception = Assert.Throws<MsmqPoisonMessageException>(() => MsmqDecodeHelper.DecodeTransportDatagram(stream, encoder, int.MaxValue));
            Assert.Contains("framing format at position 3 of stream", exception.InnerException.Message);
        }

        [Fact]
        public void DecodeMessage_ThrowException_WhenFramingModeIsNotSingletonSized()
        {
            var bindingElement = new BinaryMessageEncodingBindingElement();
            var encoder = bindingElement.CreateMessageEncoderFactory().Encoder;
            var badFrame = new byte[] { 0, 1, 0, 1, 3, 2, 37, 110, 101, 116, 46 };
            var stream = new MemoryStream(badFrame);

            var exception = Assert.Throws<MsmqPoisonMessageException>(() => MsmqDecodeHelper.DecodeTransportDatagram(stream, encoder, int.MaxValue));
            Assert.Contains("MSMQ message contained invalid or unexpected .NET Message Framing information in its body. ",
                exception.InnerException.Message);
        }

        [Fact]
        public void DecodeMessage_ThrowException_WhenDecoderSingletonSizedEOFException()
        {
            var bindingElement = new BinaryMessageEncodingBindingElement();
            var encoder = bindingElement.CreateMessageEncoderFactory().Encoder;
            var badFrame = new byte[] { 0, 1, 0, 1, 4, 2, 37, 110, 101, 116, 46 };
            var stream = new MemoryStream(badFrame);

            var exception = Assert.Throws<MsmqPoisonMessageException>(() => MsmqDecodeHelper.DecodeTransportDatagram(stream, encoder, int.MaxValue));
            Assert.Contains("More data was expected, but EOF was reached",
                exception.InnerException.InnerException.Message);
        }
    }
}
