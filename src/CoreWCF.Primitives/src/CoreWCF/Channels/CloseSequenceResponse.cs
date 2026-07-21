// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Runtime;
using System.Xml;

namespace CoreWCF.Channels
{
    internal sealed class CloseSequenceResponse : BodyWriter
    {
        private readonly UniqueId _identifier;

        public CloseSequenceResponse(UniqueId identifier)
            : base(true)
        {
            _identifier = identifier;
        }

        public static CloseSequenceResponseInfo Create(XmlDictionaryReader reader)
        {
            if (reader == null)
            {
                Fx.Assert("Argument reader cannot be null.");
            }

            CloseSequenceResponseInfo closeSequenceResponseInfo = new CloseSequenceResponseInfo();

            XmlDictionaryString wsrmNs = WsrmIndex.GetNamespace(ReliableMessagingVersion.WSReliableMessaging11);

            reader.ReadStartElement(DXD.Wsrm11Dictionary.CloseSequenceResponse, wsrmNs);
            reader.ReadStartElement(XD.WsrmFeb2005Dictionary.Identifier, wsrmNs);
            closeSequenceResponseInfo.Identifier = reader.ReadContentAsUniqueId();
            reader.ReadEndElement();

            while (reader.IsStartElement())
            {
                reader.Skip();
            }

            reader.ReadEndElement();

            return closeSequenceResponseInfo;
        }

        protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
        {
            XmlDictionaryString wsrmNs = WsrmIndex.GetNamespace(ReliableMessagingVersion.WSReliableMessaging11);

            writer.WriteStartElement(DXD.Wsrm11Dictionary.CloseSequenceResponse, wsrmNs);
            writer.WriteStartElement(XD.WsrmFeb2005Dictionary.Identifier, wsrmNs);
            writer.WriteValue(_identifier);
            writer.WriteEndElement();
            writer.WriteEndElement();
        }
    }
}
