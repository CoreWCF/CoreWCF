// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ServiceModel.Channels;
using System.Threading;
using System.Xml;

namespace Helpers.Interceptor
{
    /// <summary>
    /// Composite interceptor that mutates outbound application messages: replaces the
    /// WS-RM Sequence header with one that carries a randomly-generated bogus identifier
    /// the server has never created. The server is expected to respond with an
    /// UnknownSequenceFault (sub-code wsrm:UnknownSequence) which faults the session.
    ///
    /// Only mutates messages that carry a WSRM Sequence header (i.e. application messages
    /// post-CreateSequence); CreateSequence/Response and acks pass through unchanged so
    /// the session is allowed to start up.
    /// </summary>
    internal sealed class InjectUnknownSequenceInterceptor : IMessageInterceptor
    {
        private const string Feb2005Ns = "http://schemas.xmlsoap.org/ws/2005/02/rm";
        private const string Wsrm11Ns = "http://docs.oasis-open.org/ws-rx/wsrm/200702";
        private const string SequenceLocalName = "Sequence";

        private int _enabled;
        private int _mutatedCount;

        public bool Enabled
        {
            get => Volatile.Read(ref _enabled) != 0;
            set => Volatile.Write(ref _enabled, value ? 1 : 0);
        }

        public int MutatedCount => Volatile.Read(ref _mutatedCount);

        public InterceptDecision OnOutbound(Message message)
        {
            if (!Enabled)
            {
                return InterceptDecision.PassThrough;
            }

            int seqIndex = FindSequenceHeader(message);
            if (seqIndex < 0)
            {
                return InterceptDecision.PassThrough;
            }

            MessageHeader original = (MessageHeader)message.Headers[seqIndex];
            string ns = original.Namespace;
            string bogusId = "urn:uuid:" + Guid.NewGuid().ToString();

            ulong messageNumber = ReadSequenceNumber(message, seqIndex);
            MessageHeader replacement = new BogusSequenceHeader(ns, bogusId, messageNumber);

            // Build a replacement message: clone all headers and properties, swap the
            // sequence header. We can't mutate `message` directly because the framework
            // re-creates the forwarded message from a buffered copy on PassThrough.
            MessageBuffer buffer = message.CreateBufferedCopy(int.MaxValue);
            Message replaced = buffer.CreateMessage();
            replaced.Headers.RemoveAt(seqIndex);
            replaced.Headers.Insert(seqIndex, replacement);

            Interlocked.Increment(ref _mutatedCount);
            return InterceptDecision.Replace(replaced);
        }

        public InterceptDecision OnInbound(Message message) => InterceptDecision.PassThrough;

        private static int FindSequenceHeader(Message message)
        {
            for (int i = 0; i < message.Headers.Count; i++)
            {
                MessageHeaderInfo h = message.Headers[i];
                if (h.Name == SequenceLocalName && (h.Namespace == Feb2005Ns || h.Namespace == Wsrm11Ns))
                {
                    return i;
                }
            }
            return -1;
        }

        private static ulong ReadSequenceNumber(Message message, int seqIndex)
        {
            // Default to 1 if we can't parse for any reason; the test still drives the
            // server fault path because the identifier is wrong.
            try
            {
                using (XmlDictionaryReader reader = message.Headers.GetReaderAtHeader(seqIndex))
                {
                    reader.ReadStartElement(); // <Sequence>
                    while (reader.NodeType != XmlNodeType.EndElement)
                    {
                        if (reader.NodeType == XmlNodeType.Element &&
                            reader.LocalName == "MessageNumber")
                        {
                            return XmlConvert.ToUInt64(reader.ReadElementContentAsString());
                        }
                        reader.Skip();
                    }
                }
            }
            catch
            {
            }
            return 1;
        }

        private sealed class BogusSequenceHeader : MessageHeader
        {
            private readonly string _namespace;
            private readonly string _identifier;
            private readonly ulong _messageNumber;

            public BogusSequenceHeader(string ns, string identifier, ulong messageNumber)
            {
                _namespace = ns;
                _identifier = identifier;
                _messageNumber = messageNumber;
            }

            public override string Name => SequenceLocalName;
            public override string Namespace => _namespace;
            public override bool MustUnderstand => true;

            protected override void OnWriteStartHeader(XmlDictionaryWriter writer, MessageVersion messageVersion)
            {
                writer.WriteStartElement(Name, Namespace);
                WriteHeaderAttributes(writer, messageVersion);
            }

            protected override void OnWriteHeaderContents(XmlDictionaryWriter writer, MessageVersion messageVersion)
            {
                writer.WriteStartElement("Identifier", Namespace);
                writer.WriteString(_identifier);
                writer.WriteEndElement();
                writer.WriteStartElement("MessageNumber", Namespace);
                writer.WriteString(XmlConvert.ToString(_messageNumber));
                writer.WriteEndElement();
            }
        }
    }
}
