// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.Channels;
using Xunit;

namespace Helpers
{
    internal class TestRequestContext : RequestContext
    {
        private readonly Message _requestMessage;
        private string _replyMessageString;
        private MessageBuffer _bufferedCopy;
        private readonly ManualResetEvent _mre;

        public TestRequestContext(Message requestMessage)
        {
            _requestMessage = requestMessage;
            _mre = new ManualResetEvent(false);
        }

        public override Message RequestMessage => _requestMessage;

        public Message ReplyMessage
        {
            get
            {
                return _bufferedCopy.CreateMessage();
            }
            private set
            {
                _bufferedCopy = value.CreateBufferedCopy(int.MaxValue);
            }
        }

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
            _mre.Set();
            return Task.CompletedTask;
        }

        internal void SerializeReply()
        {
            MessageEncodingBindingElement mebe = new TextMessageEncodingBindingElement(MessageVersion.Soap11, Encoding.UTF8);
            var mef = mebe.CreateMessageEncoderFactory();
            var me = mef.Encoder;
            MemoryStream ms = new MemoryStream();
            me.WriteMessageAsync(ReplyMessage, ms);
            var messageBytes = ms.ToArray();
            _replyMessageString = Encoding.UTF8.GetString(messageBytes);
        }

        internal void ValidateReply()
        {
            Assert.Equal(s_replyMessage, _replyMessageString);
        }

        internal static TestRequestContext Create(string toAddress)
        {
            var requestMessage = TestHelper.CreateEchoRequestMessage("aaaaa");
            requestMessage.Headers.To = new Uri(toAddress);
            return new TestRequestContext(requestMessage);
        }

        internal static string s_requestMessage = @"<?xml version=""1.0"" encoding=""utf-8""?>
<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"">
  <s:Body>
    <Echo xmlns=""http://tempuri.org/"">
      <echo>aaaaa</echo>
    </Echo>
  </s:Body>
</s:Envelope>";

        private static readonly string s_replyMessage = @"<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/""><s:Header><Action s:mustUnderstand=""1"" xmlns=""http://schemas.microsoft.com/ws/2005/05/addressing/none"">http://tempuri.org/ISimpleService/EchoResponse</Action></s:Header><s:Body><EchoResponse xmlns=""http://tempuri.org/""><EchoResult>aaaaa</EchoResult></EchoResponse></s:Body></s:Envelope>";

        internal bool WaitForReply(TimeSpan timeout)
        {
            return _mre.WaitOne(timeout);
        }
    }
}
