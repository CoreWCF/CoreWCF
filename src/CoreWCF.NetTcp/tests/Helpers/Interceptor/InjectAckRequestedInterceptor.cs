// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.ServiceModel.Channels;
using System.Threading;
using System.Xml;

namespace Helpers.Interceptor
{
    /// <summary>
    /// Composite interceptor that adds an explicit AckRequested header to the next outbound
    /// application message. This drives the server's AckRequested handling in
    /// ChannelReliableSession + ReliableInputConnection, which is otherwise only fired by
    /// client-side timer/heartbeat code that may not run within a fast unit test.
    /// </summary>
    internal sealed class InjectAckRequestedInterceptor : IMessageInterceptor
    {
        private const string Feb2005Ns = "http://schemas.xmlsoap.org/ws/2005/02/rm";
        private const string Wsrm11Ns = "http://docs.oasis-open.org/ws-rx/wsrm/200702";
        private const string SequenceLocalName = "Sequence";
        private const string AckRequestedLocalName = "AckRequested";

        private int _enabled;
        private int _injectedCount;

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

            // Find the wsrm:Sequence header so we can copy its identifier and namespace.
            int seqIndex = -1;
            for (int i = 0; i < message.Headers.Count; i++)
            {
                MessageHeaderInfo h = message.Headers[i];
                if (h.Name == SequenceLocalName && (h.Namespace == Feb2005Ns || h.Namespace == Wsrm11Ns))
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
            string identifier = ReadSequenceIdentifier(message, seqIndex);
            if (identifier == null)
            {
                return InterceptDecision.PassThrough;
            }

            MessageBuffer buffer = message.CreateBufferedCopy(int.MaxValue);
            Message replaced = buffer.CreateMessage();
            replaced.Headers.Add(new AckRequestedHeader(ns, identifier));

            Interlocked.Increment(ref _injectedCount);
            return InterceptDecision.Replace(replaced);
        }

        public InterceptDecision OnInbound(Message message) => InterceptDecision.PassThrough;

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

        private sealed class AckRequestedHeader : MessageHeader
        {
            private readonly string _namespace;
            private readonly string _identifier;

            public AckRequestedHeader(string ns, string identifier)
            {
                _namespace = ns;
                _identifier = identifier;
            }

            public override string Name => AckRequestedLocalName;
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

                // Feb2005 also requires <MessageNumber> inside AckRequested; for WSRM 1.1 it
                // is forbidden. We always write it for Feb2005 only.
                if (_namespace == Feb2005Ns)
                {
                    writer.WriteStartElement("MessageNumber", Namespace);
                    writer.WriteString("1");
                    writer.WriteEndElement();
                }
            }
        }
    }
}
