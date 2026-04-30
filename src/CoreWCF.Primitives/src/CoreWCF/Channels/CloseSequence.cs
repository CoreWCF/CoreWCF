// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    internal sealed class CloseSequence : BodyWriter
    {
        private readonly UniqueId _identifier;
        private readonly long _lastMsgNumber;

        public CloseSequence(UniqueId identifier, long lastMsgNumber)
            : base(true)
        {
            _identifier = identifier;
            _lastMsgNumber = lastMsgNumber;
        }

        public static CloseSequenceInfo Create(XmlDictionaryReader reader)
        {
            if (reader == null)
            {
                Fx.Assert("Argument reader cannot be null.");
            }

            CloseSequenceInfo closeSequenceInfo = new CloseSequenceInfo();

            XmlDictionaryString wsrmNs = WsrmIndex.GetNamespace(ReliableMessagingVersion.WSReliableMessaging11);
            Wsrm11Dictionary wsrm11Dictionary = DXD.Wsrm11Dictionary;

            reader.ReadStartElement(wsrm11Dictionary.CloseSequence, wsrmNs);
            reader.ReadStartElement(XD.WsrmFeb2005Dictionary.Identifier, wsrmNs);
            closeSequenceInfo.Identifier = reader.ReadContentAsUniqueId();
            reader.ReadEndElement();

            if (reader.IsStartElement(wsrm11Dictionary.LastMsgNumber, wsrmNs))
            {
                reader.ReadStartElement();
                closeSequenceInfo.LastMsgNumber = WsrmUtilities.ReadSequenceNumber(reader, false);
                reader.ReadEndElement();
            }

            while (reader.IsStartElement())
            {
                reader.Skip();
            }

            reader.ReadEndElement();

            return closeSequenceInfo;
        }

        protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
        {
            XmlDictionaryString wsrmNs = WsrmIndex.GetNamespace(ReliableMessagingVersion.WSReliableMessaging11);
            Wsrm11Dictionary wsrm11Dictionary = DXD.Wsrm11Dictionary;

            writer.WriteStartElement(wsrm11Dictionary.CloseSequence, wsrmNs);
            writer.WriteStartElement(XD.WsrmFeb2005Dictionary.Identifier, wsrmNs);
            writer.WriteValue(_identifier);
            writer.WriteEndElement();

            if (_lastMsgNumber > 0)
            {
                writer.WriteStartElement(wsrm11Dictionary.LastMsgNumber, wsrmNs);
                writer.WriteValue(_lastMsgNumber);
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }
    }
}
