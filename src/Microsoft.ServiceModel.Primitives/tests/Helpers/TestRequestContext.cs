using Microsoft.ServiceModel.Channels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Helpers
{
    class TestRequestContext : RequestContext
    {
        private Message _requestMessage;
        private string _replyMessageString;

        public TestRequestContext(Message requestMessage)
        {
            _requestMessage = requestMessage;
        }

        public override Message RequestMessage => _requestMessage;

        public Message ReplyMessage { get; private set; }

        public override void Abort()
        {
        }

        public override Task CloseAsync()
        {
            return Task.CompletedTask;
        }

        public override Task CloseAsync(CancellationToken token)
        {
            return Task.CompletedTask;
        }

        public override Task ReplyAsync(Message message)
        {
            return ReplyAsync(message, CancellationToken.None);
        }

        public override Task ReplyAsync(Message message, CancellationToken token)
        {
            ReplyMessage = message;
            SerializeReply();
            return Task.CompletedTask;
        }

        internal void SerializeReply()
        {
            MessageEncodingBindingElement mebe = new TextMessageEncodingBindingElement(MessageVersion.Soap11, Encoding.UTF8);
            var mef = mebe.CreateMessageEncoderFactory();
            var me = mef.Encoder;
            MemoryStream ms = new MemoryStream();
            me.WriteMessage(ReplyMessage, ms);
            var messageBytes = ms.ToArray();
            _replyMessageString = Encoding.UTF8.GetString(messageBytes);
        }

        internal void ValidateReply()
        {
            Assert.Equal(s_replyMessage, _replyMessageString);
        }

        internal static TestRequestContext Create(string toAddress)
        {
            MessageEncodingBindingElement mebe = new TextMessageEncodingBindingElement(MessageVersion.Soap11, Encoding.UTF8);
            var mef = mebe.CreateMessageEncoderFactory();
            var me = mef.Encoder;
            var requestMessageBytes = Encoding.UTF8.GetBytes(s_requestMessage);
            var requestMessage = me.ReadMessage(new ArraySegment<byte>(requestMessageBytes), BufferManager.CreateBufferManager(1, 1));
            requestMessage.Headers.To = new Uri(toAddress);
            requestMessage.Headers.Action = "http://tempuri.org/ISimpleService/Echo";
            return new TestRequestContext(requestMessage);
        }

        private static string s_requestMessage = @"<?xml version=""1.0"" encoding=""utf-8""?>
<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"">
  <s:Body>
    <Echo xmlns=""http://tempuri.org/"">
      <echo>aaaaa</echo>
    </Echo>
  </s:Body>
</s:Envelope>";

        private static string s_replyMessage = @"<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/""><s:Header><Action s:mustUnderstand=""1"" xmlns=""http://schemas.microsoft.com/ws/2005/05/addressing/none"">http://tempuri.org/ISimpleService/EchoResponse</Action></s:Header><s:Body><EchoResponse xmlns=""http://tempuri.org/""><EchoResult>aaaaa</EchoResult></EchoResponse></s:Body></s:Envelope>";
    }
}
