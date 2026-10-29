// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ServiceModel.Channels;
using System.Threading;
using System.Xml;

namespace Helpers.Interceptor
{
    /// <summary>
    /// Injects a wsrm:SequenceAcknowledgement header on the next outbound application
    /// message that acknowledges a *future* message number (one the server has never sent).
    /// Per the WS-RM spec the receiver of such an invalid ack must respond with
    /// InvalidAcknowledgement (sub-code wsrm:InvalidAcknowledgement). This drives the
    /// InvalidAcknowledgementFault construction + serialization path in WsrmFault.
    /// </summary>
    internal sealed class InjectInvalidAckInterceptor : IMessageInterceptor
    {
        private const string Feb2005Ns = "http://schemas.xmlsoap.org/ws/2005/02/rm";
        private const string Wsrm11Ns = "http://docs.oasis-open.org/ws-rx/wsrm/200702";

        private int _enabled;
        private int _injectedCount;
        private string _captureSequenceId;

        public bool Enabled
        {
            get => Volatile.Read(ref _enabled) != 0;
            set => Volatile.Write(ref _enabled, value ? 1 : 0);
        }

        public int InjectedCount => Volatile.Read(ref _injectedCount);

        public InterceptDecision OnOutbound(Message message)
        {
            if (!Enabled)
            {
                return InterceptDecision.PassThrough;
            }

            int seqIndex = -1;
            for (int i = 0; i < message.Headers.Count; i++)
            {
                MessageHeaderInfo h = message.Headers[i];
                if (h.Name == "Sequence" && (h.Namespace == Feb2005Ns || h.Namespace == Wsrm11Ns))
                {
                    seqIndex = i;
                    break;
                }
            }
            if (seqIndex < 0)
            {
                return InterceptDecision.PassThrough;
            }

            string ns = message.Headers[seqIndex].Namespace;
            // Use the captured server-side sequence id if available; otherwise fall back to
            // the client's outgoing sequence id (which the server WILL recognize, making
            // the ack on a future number actually invalid for the sequence it claims to ack).
            string sequenceId = Volatile.Read(ref _captureSequenceId) ?? ReadSequenceIdentifier(message, seqIndex);
            if (sequenceId == null)
            {
                return InterceptDecision.PassThrough;
            }

            MessageBuffer buffer = message.CreateBufferedCopy(int.MaxValue);
            Message replaced = buffer.CreateMessage();
            // Acknowledge a sequence number that the OTHER side has not sent. Range
            // [1000..1000] is well above anything the test exchange will produce.
            replaced.Headers.Add(new InvalidAckHeader(ns, sequenceId, 1000, 1000));

            Interlocked.Increment(ref _injectedCount);
            return InterceptDecision.Replace(replaced);
        }

        public InterceptDecision OnInbound(Message message)
        {
            // Capture the server-issued sequence id from CreateSequenceResponse so that
            // when we later inject an ack we can target the correct sequence.
            if (Volatile.Read(ref _captureSequenceId) == null)
            {
                string action = message.Headers.Action;
                if (action != null && action.EndsWith("/CreateSequenceResponse", StringComparison.Ordinal))
                {
                    string id = ExtractIdentifierFromBody(message);
                    if (id != null)
                    {
                        Interlocked.CompareExchange(ref _captureSequenceId, id, null);
                    }
                }
            }
            return InterceptDecision.PassThrough;
        }

        private static string ReadSequenceIdentifier(Message message, int seqIndex)
        {
            try
            {
                using (XmlDictionaryReader reader = message.Headers.GetReaderAtHeader(seqIndex))
                {
                    reader.ReadStartElement();
                    while (reader.NodeType != XmlNodeType.EndElement)
                    {
                        if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "Identifier")
                        {
                            return reader.ReadElementContentAsString();
                        }
                        reader.Skip();
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        private static string ExtractIdentifierFromBody(Message message)
        {
            try
            {
                MessageBuffer buf = message.CreateBufferedCopy(int.MaxValue);
                // Replace the original body with a fresh one for forwarding (caller will use
                // buffer.CreateMessage()), then read identifier from a separate copy.
                using (Message scan = buf.CreateMessage())
                using (XmlDictionaryReader reader = scan.GetReaderAtBodyContents())
                {
                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "Identifier")
                        {
                            return reader.ReadElementContentAsString();
                        }
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        private sealed class InvalidAckHeader : MessageHeader
        {
            private readonly string _namespace;
            private readonly string _identifier;
            private readonly long _lower;
            private readonly long _upper;

            public InvalidAckHeader(string ns, string identifier, long lower, long upper)
            {
                _namespace = ns;
                _identifier = identifier;
                _lower = lower;
                _upper = upper;
            }

            public override string Name => "SequenceAcknowledgement";
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

                writer.WriteStartElement("AcknowledgementRange", Namespace);
                writer.WriteAttributeString("Lower", XmlConvert.ToString(_lower));
                writer.WriteAttributeString("Upper", XmlConvert.ToString(_upper));
                writer.WriteEndElement();
            }
        }
    }
}
