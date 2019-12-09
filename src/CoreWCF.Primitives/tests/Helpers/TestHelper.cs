﻿using CoreWCF;
using CoreWCF.Channels;
using System;
using System.IO;
using System.Text;
using System.Xml;

namespace Helpers
{
    public static class TestHelper
    {
        private const string EchoAction = "http://tempuri.org/ISimpleService/Echo";
        private static string s_echoPrefix = @"<?xml version=""1.0"" encoding=""utf-8""?>
<s:Envelope xmlns:s=""http://schemas.xmlsoap.org/soap/envelope/"">
  <s:Body>
    <Echo xmlns = ""http://tempuri.org/"">
      <echo>";
        private static string s_echoSuffix = @"</echo>
    </Echo>
  </s:Body>
</s:Envelope>";

        public static Message CreateEchoRequestMessage(string echo)
        {
            string requestMessageStr = s_echoPrefix + echo + s_echoSuffix;
            var xmlDictionaryReader = XmlDictionaryReader.CreateTextReader(Encoding.UTF8.GetBytes(requestMessageStr), XmlDictionaryReaderQuotas.Max);
            var requestMessage = Message.CreateMessage(xmlDictionaryReader, int.MaxValue, MessageVersion.Soap11);
            requestMessage.Headers.Action = EchoAction;
            return requestMessage;
        }

        internal static System.ServiceModel.Channels.Message ConvertMessage(Message message)
        {
            var ms = SerializeMessageToStream(message);
            var convertedMessage = DeserialzieMessageFromStream(ms, ConvertMessageVersion(message.Version));
            convertedMessage.Headers.To = message.Headers.To;
            return convertedMessage;
        }

        internal static Message ConvertMessage(System.ServiceModel.Channels.Message message)
        {
            var ms = SerializeMessageToStream(message);
            var convertedMessage = DeserialzieMessageFromStream(ms, ConvertMessageVersion(message.Version));
            convertedMessage.Headers.To = message.Headers.To;
            return convertedMessage;
        }

        private static MessageVersion ConvertMessageVersion(System.ServiceModel.Channels.MessageVersion version)
        {
            EnvelopeVersion envelopeVersion = null;
            if (System.ServiceModel.EnvelopeVersion.None.Equals(version.Envelope))
            {
                envelopeVersion = EnvelopeVersion.None;
            }
            else if (System.ServiceModel.EnvelopeVersion.Soap11.Equals(version.Envelope))
            {
                envelopeVersion = EnvelopeVersion.Soap11;
            }
            else if (System.ServiceModel.EnvelopeVersion.Soap12.Equals(version.Envelope))
            {
                envelopeVersion = EnvelopeVersion.Soap12;
            }

            AddressingVersion addressingVersion = null;
            if (System.ServiceModel.Channels.AddressingVersion.None.Equals(version.Addressing))
            {
                addressingVersion = AddressingVersion.None;
            }
            else if (System.ServiceModel.Channels.AddressingVersion.WSAddressing10.Equals(version.Addressing))
            {
                addressingVersion = AddressingVersion.WSAddressing10;
            }
            else if (System.ServiceModel.Channels.AddressingVersion.WSAddressingAugust2004.Equals(version.Addressing))
            {
                addressingVersion = AddressingVersion.WSAddressingAugust2004;
            }

            return MessageVersion.CreateVersion(envelopeVersion, addressingVersion);
        }

        private static System.ServiceModel.Channels.MessageVersion ConvertMessageVersion(MessageVersion version)
        {
            System.ServiceModel.EnvelopeVersion envelopeVersion = null;
            if (EnvelopeVersion.None.Equals(version.Envelope))
            {
                envelopeVersion = System.ServiceModel.EnvelopeVersion.None;
            }
            else if (EnvelopeVersion.Soap11.Equals(version.Envelope))
            {
                envelopeVersion = System.ServiceModel.EnvelopeVersion.Soap11;
            }
            else if (EnvelopeVersion.Soap12.Equals(version.Envelope))
            {
                envelopeVersion = System.ServiceModel.EnvelopeVersion.Soap12;
            }

            System.ServiceModel.Channels.AddressingVersion addressingVersion = null;
            if (AddressingVersion.None.Equals(version.Addressing))
            {
                addressingVersion = System.ServiceModel.Channels.AddressingVersion.None;
            }
            else if (AddressingVersion.WSAddressing10.Equals(version.Addressing))
            {
                addressingVersion = System.ServiceModel.Channels.AddressingVersion.WSAddressing10;
            }
            else if (AddressingVersion.WSAddressingAugust2004.Equals(version.Addressing))
            {
                addressingVersion = System.ServiceModel.Channels.AddressingVersion.WSAddressingAugust2004;
            }

            return System.ServiceModel.Channels.MessageVersion.CreateVersion(envelopeVersion, addressingVersion);
        }

        private static Message DeserialzieMessageFromStream(MemoryStream ms, MessageVersion messageVersion)
        {
            var bmebe = new BinaryMessageEncodingBindingElement();
            bmebe.MessageVersion = messageVersion;
            bmebe.ReaderQuotas = XmlDictionaryReaderQuotas.Max;
            var bmef = bmebe.CreateMessageEncoderFactory();
            return bmef.Encoder.ReadMessage(ms, int.MaxValue);
        }

        private static MemoryStream SerializeMessageToStream(System.ServiceModel.Channels.Message requestMessage)
        {
            var bmebe = new System.ServiceModel.Channels.BinaryMessageEncodingBindingElement();
            bmebe.MessageVersion = requestMessage.Version;
            bmebe.ReaderQuotas = XmlDictionaryReaderQuotas.Max;
            var bmef = bmebe.CreateMessageEncoderFactory();
            var ms = new MemoryStream(64 * 1024); // 64K to keep out of LOH
            bmef.Encoder.WriteMessage(requestMessage, ms);
            ms.Position = 0;
            return ms;
        }

        private static System.ServiceModel.Channels.Message DeserialzieMessageFromStream(MemoryStream ms, System.ServiceModel.Channels.MessageVersion messageVersion)
        {
            var bmebe = new System.ServiceModel.Channels.BinaryMessageEncodingBindingElement();
            bmebe.MessageVersion = messageVersion;
            bmebe.ReaderQuotas = XmlDictionaryReaderQuotas.Max;
            var bmef = bmebe.CreateMessageEncoderFactory();
            return bmef.Encoder.ReadMessage(ms, int.MaxValue);
        }

        private static MemoryStream SerializeMessageToStream(Message requestMessage)
        {
            var bmebe = new BinaryMessageEncodingBindingElement();
            bmebe.MessageVersion = requestMessage.Version;
            bmebe.ReaderQuotas = XmlDictionaryReaderQuotas.Max;
            var bmef = bmebe.CreateMessageEncoderFactory();
            var ms = new MemoryStream(64 * 1024); // 64K to keep out of LOH
            bmef.Encoder.WriteMessage(requestMessage, ms);
            ms.Position = 0;
            return ms;
        }

        internal static void CloseServiceModelObjects(params System.ServiceModel.ICommunicationObject[] objects)
        {
            foreach (System.ServiceModel.ICommunicationObject comObj in objects)
            {
                try
                {
                    if (comObj == null)
                    {
                        continue;
                    }
                    // Only want to call Close if it is in the Opened state
                    if (comObj.State == System.ServiceModel.CommunicationState.Opened)
                    {
                        comObj.Close();
                    }
                    // Anything not closed by this point should be aborted
                    if (comObj.State != System.ServiceModel.CommunicationState.Closed)
                    {
                        comObj.Abort();
                    }
                }
                catch (TimeoutException)
                {
                    comObj.Abort();
                }
                catch (System.ServiceModel.CommunicationException)
                {
                    comObj.Abort();
                }
            }
        }
    }
}
