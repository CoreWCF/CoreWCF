// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using CoreWCF.Runtime;
using System.Xml;

namespace CoreWCF.Channels
{
    internal sealed class TerminateSequenceResponse : BodyWriter
    {
        public TerminateSequenceResponse() : base(true) { }

        public TerminateSequenceResponse(UniqueId identifier)
            : base(true)
        {
            Identifier = identifier;
        }

        public UniqueId Identifier { get; set; }

        public static TerminateSequenceResponseInfo Create(XmlDictionaryReader reader)
        {
            Fx.Assert(reader != null, "Argument reader cannot be null.");

            TerminateSequenceResponseInfo terminateSequenceInfo = new TerminateSequenceResponseInfo();
            XmlDictionaryString wsrmNs = WsrmIndex.GetNamespace(ReliableMessagingVersion.WSReliableMessaging11);

            reader.ReadStartElement(DXD.Wsrm11Dictionary.TerminateSequenceResponse, wsrmNs);
            reader.ReadStartElement(XD.WsrmFeb2005Dictionary.Identifier, wsrmNs);
            terminateSequenceInfo.Identifier = reader.ReadContentAsUniqueId();
            reader.ReadEndElement();

            while (reader.IsStartElement())
            {
                reader.Skip();
            }

            reader.ReadEndElement();

            return terminateSequenceInfo;
        }

        protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
        {
            XmlDictionaryString wsrmNs = WsrmIndex.GetNamespace(ReliableMessagingVersion.WSReliableMessaging11);
            writer.WriteStartElement(DXD.Wsrm11Dictionary.TerminateSequenceResponse, wsrmNs);
            writer.WriteStartElement(XD.WsrmFeb2005Dictionary.Identifier, wsrmNs);
            writer.WriteValue(Identifier);
            writer.WriteEndElement();
            writer.WriteEndElement();
        }
    }
}
