using CoreWCF.Channels;
using Helpers;
using System;
using System.IO;
using System.Text;
using System.Xml;
using Xunit;

namespace CoreWCF.Primitives.Tests
{
    public class MessageEncoderTest
    {
        [Fact]
        public void BasicTextTest()
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
                    f.WriteMessage(myMessage, s);
                    s.Seek(0, SeekOrigin.Begin);
                    _ = new StreamReader(s);
                    s.Seek(0, SeekOrigin.Begin);
                    Message m2 = f.ReadMessage(s, int.MaxValue);

                    // original got closed by sending, so recreate it:
                    myMessage = ims.CurrentParameters.CreateMessage();
                    if (Helpers.MessageTestUtilities.AreMessagesEqual(myMessage, m2))
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
            catch(Exception ex)
            {
                Assert.True(false, $"Exception caught: {ex.Message} More information: {bad} bad ones, {good} good ones.");
            }           
        }

        [Fact]
        public void BodyWriterMessageTest()
        {
            string action = "http://www.action.com/";
            Message m1 = Message.CreateMessage(MessageVersion.Soap12WSAddressing10, action, new CustomGeneratedBodyWriter(2, 1024));
            Message m1p = Message.CreateMessage(MessageVersion.Soap12WSAddressing10, action, new CustomGeneratedBodyWriter(2, 1024));

            // Note, m1 is closed by this, which is we compare m2 with m1p
            Message m2 = Helpers.MessageTestUtilities.SendAndReceiveMessage(m1);
            Assert.True(Helpers.MessageTestUtilities.AreBodiesEqual(m1p, m2, true, true));
        }

        [Fact]
        public void ObjectHeaderTests()
        {         
            Message message = Message.CreateMessage(MessageVersion.Soap12WSAddressing10, "http://www.action.com/");
            GeneratedSerializableObject generatedSerializableObject = new GeneratedSerializableObject(2, 200L);
            MessageHeader header = MessageHeader.CreateHeader("foo", "", generatedSerializableObject);
            message.Headers.Add(header);
            Message message2 = Helpers.MessageTestUtilities.SendAndReceiveMessage(message);
            int num = message2.Headers.FindHeader("foo", "");
            Assert.NotEqual(-1, num);
          
            object header2 = message2.Headers.GetHeader<GeneratedSerializableObject>(num);
            object header3 = message2.Headers.GetHeader<GeneratedSerializableObject>("foo", "");
            Assert.False(!generatedSerializableObject.Equals(header2) || !generatedSerializableObject.Equals(header3));
           
            string s = "<s:Envelope xmlns:s=\"http://www.w3.org/2003/05/soap-envelope\" xmlns:a=\"http://schemas.xmlsoap.org/ws/2003/03/addressing\"><s:Header /><s:Body /></s:Envelope>";
            message = Message.CreateMessage(new XmlTextReader(new StringReader(s)), 2147483647, MessageVersion.Default);
            message2 = Message.CreateMessage(new XmlTextReader(new StringReader(s)), 2147483647, MessageVersion.Default);
            Message two = MessageTestUtilities.SendAndReceiveMessage(message);
            Assert.True(Helpers.MessageTestUtilities.AreBodiesEqual(message2, two));             
        }        
    }
}
