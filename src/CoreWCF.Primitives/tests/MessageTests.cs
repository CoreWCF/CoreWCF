using CoreWCF.Channels;
using System;
using System.Text;
using Xunit;

namespace CoreWCF.Primitives.Tests
{
    public static class MessageTests
    {
        [Fact]
        public static void InvalidCharToString()
        {
            string invalidSoap11Message = @"<?xml version=""1.0"" encoding=""utf-8""?>
<soap:Envelope xmlns:xsi=""http://www.w3.org/2001/XMLSchema-instance"" xmlns:soap=""http://schemas.xmlsoap.org/soap/envelope/"">
<soap:Body>
<ServiceResponse xmlns=""http://tempuri.org/"">
<ServiceResult>string" + (char)0x0f + @"</ServiceResult>
</ServiceResponse>
</soap:Body>
</soap:Envelope>";
            var textMessageEncodingBindingElement = new TextMessageEncodingBindingElement { MessageVersion = MessageVersion.Soap11 };
            var factory = textMessageEncodingBindingElement.CreateMessageEncoderFactory();
            var messageEncoder = factory.Encoder;
            var messageBytes = Encoding.UTF8.GetBytes(invalidSoap11Message);
            var message = messageEncoder.ReadMessage(new ArraySegment<byte>(messageBytes), BufferManager.CreateBufferManager(10, 10), "text/xml; charset=utf-8");
            var messageStr = message.ToString();
            Assert.NotNull(messageStr);
            Assert.Contains("The byte 0x0F is not valid at this location", messageStr);
        }
    }
}
