using CoreWCF.Channels;
using System;
using System.Collections.Generic;
using Xunit;
using Xunit.Abstractions;

namespace CoreWCF.Primitives.Tests
{
    public class BasicFaultTest
    {
        [Fact]
        public void RunBasicFaultTest()
        {
            for
            (
                MessageVersion version = MessageVersion.Soap11;
                version != null;
                version = (version == MessageVersion.Soap11) ? MessageVersion.Soap12WSAddressing10 : null
            )
            {
                var translations = new List<FaultReasonText>();
                translations.Add(new FaultReasonText("Reason: auto-generated fault for testing.", "en-us"));
                translations.Add(new FaultReasonText("Raison: auto-generat error pour examiner.", "fr"));

                var reason = new FaultReason(translations);
                Object detail = "Sample fault detail content.";

                MessageFault fault = MessageFault.CreateFault(new FaultCode("Sender"), reason, detail, new System.Runtime.Serialization.DataContractSerializer(typeof(string)), "", "");
                Message message = Message.CreateMessage(MessageVersion.Soap12WSAddressing10, fault, "http://www.w3.org/2005/08/addressing/fault");
                Message m2 = MessageTestUtilities.SendAndReceiveMessage(message);
                MessageFault f2 = MessageFault.CreateFault(m2, int.MaxValue);

                IsFaultEqual(fault, f2);
            }
        }

        private void IsFaultEqual(MessageFault f1, MessageFault f2)
        {
            if (f1.Code.Name != f2.Code.Name)
            {
                throw new ApplicationException("Message Fault Names are not equal");
            }

            if (f1.Reason.ToString() != f2.Reason.ToString())
            {
                throw new ApplicationException("Message Fault Reason are not equal");
            }

            if (f1.Node != f2.Node)
            {
                throw new ApplicationException("Message Fault Node are not equal");
            }

            if (f1.Actor != f2.Actor)
            {
                throw new ApplicationException("Message Fault Actor are not equal");
            }

            if (!(f1.HasDetail ^ f2.HasDetail))
            {
                if (f1.GetDetail<String>() != f2.GetDetail<String>())
                {
                    throw new ApplicationException("Message Fault Detail are not equal");
                }
            }
        }
    }

    internal class MessageTestUtilities
    {
        public static Message SendAndReceiveMessage(Message toSend)
        {
            MessageEncoder encoder = null;
            if (toSend.Version.Envelope == EnvelopeVersion.Soap11)
            {
                encoder = new TextMessageEncodingBindingElement(toSend.Version, System.Text.Encoding.UTF8).CreateMessageEncoderFactory().Encoder;
            }
            else
            {
                encoder = new BinaryMessageEncodingBindingElement().CreateMessageEncoderFactory().Encoder;
            }
            BufferManager bufferManager = BufferManager.CreateBufferManager(int.MaxValue, int.MaxValue);
            ArraySegment<byte> encodedMessage = encoder.WriteMessage(toSend, int.MaxValue, bufferManager);

            Message r = encoder.ReadMessage(encodedMessage, bufferManager);
            return r;
        }
    }
}