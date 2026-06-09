// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Xml;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    internal abstract class ReceivedMessage : Message
    {
        private bool _isFault;
        private bool _isEmpty;

        public override bool IsEmpty
        {
            get { return _isEmpty; }
        }

        public override bool IsFault
        {
            get { return _isFault; }
        }

        protected static bool HasHeaderElement(XmlDictionaryReader reader, EnvelopeVersion envelopeVersion)
        {
            return reader.IsStartElement(XD.MessageDictionary.Header, envelopeVersion.DictionaryNamespace);
        }

        protected override void OnWriteBodyContents(XmlDictionaryWriter writer)
        {
            if (!_isEmpty)
            {
                using (XmlDictionaryReader bodyReader = OnGetReaderAtBodyContents())
                {
                    if (bodyReader.ReadState == ReadState.Error || bodyReader.ReadState == ReadState.Closed)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.MessageBodyReaderInvalidReadState, bodyReader.ReadState.ToString())));
                    }

                    while (bodyReader.NodeType != XmlNodeType.EndElement && !bodyReader.EOF)
                    {
                        writer.WriteNode(bodyReader, false);
                    }

                    ReadFromBodyContentsToEnd(bodyReader, Version.Envelope);
                }
            }
        }

        protected bool ReadStartBody(XmlDictionaryReader reader)
        {
            return ReadStartBody(reader, Version.Envelope, out _isFault, out _isEmpty);
        }

        protected static EnvelopeVersion ReadStartEnvelope(XmlDictionaryReader reader)
        {
            EnvelopeVersion envelopeVersion;

            if (reader.IsStartElement(XD.MessageDictionary.Envelope, XD.Message12Dictionary.Namespace))
            {
                envelopeVersion = EnvelopeVersion.Soap12;
            }
            else if (reader.IsStartElement(XD.MessageDictionary.Envelope, XD.Message11Dictionary.Namespace))
            {
                envelopeVersion = EnvelopeVersion.Soap11;
            }
            else
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CommunicationException(SR.MessageVersionUnknown));
            }

            if (reader.IsEmptyElement)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CommunicationException(SR.MessageBodyMissing));
            }

            reader.Read();
            return envelopeVersion;
        }

        protected static void VerifyStartBody(XmlDictionaryReader reader, EnvelopeVersion version)
        {
            if (!reader.IsStartElement(XD.MessageDictionary.Body, version.DictionaryNamespace))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CommunicationException(SR.MessageBodyMissing));
            }
        }

        private static void ReadFromBodyContentsToEnd(XmlDictionaryReader reader, EnvelopeVersion envelopeVersion)
        {
            if (envelopeVersion != EnvelopeVersion.None)
            {
                reader.ReadEndElement(); // </Body>
                reader.ReadEndElement(); // </Envelope>
            }
            reader.MoveToContent();
        }

        internal static bool ReadStartBody(XmlDictionaryReader reader, EnvelopeVersion envelopeVersion, out bool isFault, out bool isEmpty)
        {
            if (reader.IsEmptyElement)
            {
                reader.Read();
                isEmpty = true;
                isFault = false;
                reader.ReadEndElement();
                return false;
            }
            else
            {
                reader.Read();
                if (reader.NodeType != XmlNodeType.Element)
                {
                    reader.MoveToContent();
                }

                if (reader.NodeType == XmlNodeType.Element)
                {
                    isFault = IsFaultStartElement(reader, envelopeVersion);
                    isEmpty = false;
                }
                else if (reader.NodeType == XmlNodeType.EndElement)
                {
                    isEmpty = true;
                    isFault = false;
                    ReadFromBodyContentsToEnd(reader, envelopeVersion);
                    return false;
                }
                else
                {
                    isEmpty = false;
                    isFault = false;
                }

                return true;
            }
        }

        internal static bool IsFaultStartElement(XmlDictionaryReader reader, EnvelopeVersion version)
        {
            return reader.IsStartElement(XD.MessageDictionary.Fault, version.DictionaryNamespace);
        }
    }

    internal sealed class BufferedMessage : ReceivedMessage
    {
        private readonly MessageHeaders _headers;
        private readonly MessageProperties _properties;
        private RecycledMessageState _recycledMessageState;
        private XmlDictionaryReader _reader;
        private readonly XmlAttributeHolder[] _bodyAttributes;

        public BufferedMessage(IBufferedMessageData2 messageData, RecycledMessageState recycledMessageState)
            : this(messageData, recycledMessageState, null, false)
        {
        }

        public BufferedMessage(IBufferedMessageData2 messageData, RecycledMessageState recycledMessageState, bool[] understoodHeaders, bool understoodHeadersModified)
        {
            //bool throwing = true;
            //try
            //{
            _recycledMessageState = recycledMessageState;
            MessageData = messageData;
            _properties = recycledMessageState.TakeProperties();
            if (_properties == null)
            {
                _properties = new MessageProperties();
            }

            XmlDictionaryReader reader = messageData.GetMessageReader();
            MessageVersion desiredVersion = messageData.MessageEncoder.MessageVersion;

            if (desiredVersion.Envelope == EnvelopeVersion.None)
            {
                _reader = reader;
                _headers = new MessageHeaders(desiredVersion);
            }
            else
            {
                EnvelopeVersion envelopeVersion = ReadStartEnvelope(reader);
                if (desiredVersion.Envelope != envelopeVersion)
                {
                    Exception versionMismatchException = new ArgumentException(SR.Format(SR.EncoderEnvelopeVersionMismatch, envelopeVersion, desiredVersion.Envelope), "reader");
                    throw TraceUtility.ThrowHelperError(
                        new CommunicationException(versionMismatchException.Message, versionMismatchException),
                        this);
                }

                if (HasHeaderElement(reader, envelopeVersion))
                {
                    _headers = recycledMessageState.TakeHeaders();
                    if (_headers == null)
                    {
                        _headers = new MessageHeaders(desiredVersion, reader, messageData, recycledMessageState, understoodHeaders, understoodHeadersModified);
                    }
                    else
                    {
                        _headers.Init(desiredVersion, reader, messageData, recycledMessageState, understoodHeaders, understoodHeadersModified);
                    }
                }
                else
                {
                    _headers = new MessageHeaders(desiredVersion);
                }

                VerifyStartBody(reader, envelopeVersion);

                int maxSizeOfAttributes = int.MaxValue;
                _bodyAttributes = XmlAttributeHolder.ReadAttributes(reader, ref maxSizeOfAttributes);
                if (maxSizeOfAttributes < int.MaxValue - 4096)
                {
                    _bodyAttributes = null;
                }

                if (ReadStartBody(reader))
                {
                    _reader = reader;
                }
                else
                {
                    reader.Dispose();
                }
            }
            //throwing = false;
            //}
            //finally
            //{
            //    if (throwing && MessageLogger.LoggingEnabled)
            //    {
            //        MessageLogger.LogMessage(messageData.Buffer, MessageLoggingSource.Malformed);
            //    }
            //}
        }

        public override MessageHeaders Headers
        {
            get
            {
                if (IsDisposed)
                {
                    throw TraceUtility.ThrowHelperError(this.CreateMessageDisposedException(), this);
                }

                return _headers;
            }
        }

        internal IBufferedMessageData2 MessageData { get; private set; }

        public override MessageProperties Properties
        {
            get
            {
                if (IsDisposed)
                {
                    throw TraceUtility.ThrowHelperError(this.CreateMessageDisposedException(), this);
                }

                return _properties;
            }
        }

        public override RecycledMessageState RecycledMessageState
        {
            get { return _recycledMessageState; }
        }

        public override MessageVersion Version
        {
            get
            {
                return _headers.MessageVersion;
            }
        }

        protected override XmlDictionaryReader OnGetReaderAtBodyContents()
        {
            XmlDictionaryReader reader = _reader;
            _reader = null;
            return reader;
        }

        public override XmlDictionaryReader GetReaderAtHeader()
        {
            if (!_headers.ContainsOnlyBufferedMessageHeaders)
            {
                return base.GetReaderAtHeader();
            }

            XmlDictionaryReader reader = MessageData.GetMessageReader();
            if (reader.NodeType != XmlNodeType.Element)
            {
                reader.MoveToContent();
            }

            reader.Read();
            if (HasHeaderElement(reader, _headers.MessageVersion.Envelope))
            {
                return reader;
            }

            return base.GetReaderAtHeader();
        }

        public XmlDictionaryReader GetBufferedReaderAtBody()
        {
            XmlDictionaryReader reader = MessageData.GetMessageReader();
            if (reader.NodeType != XmlNodeType.Element)
            {
                reader.MoveToContent();
            }

            if (Version.Envelope != EnvelopeVersion.None)
            {
                reader.Read();
                if (HasHeaderElement(reader, _headers.MessageVersion.Envelope))
                {
                    reader.Skip();
                }

                if (reader.NodeType != XmlNodeType.Element)
                {
                    reader.MoveToContent();
                }
            }
            return reader;
        }

        public XmlDictionaryReader GetMessageReader()
        {
            return MessageData.GetMessageReader();
        }

        protected override void OnBodyToString(XmlDictionaryWriter writer)
        {
            using (XmlDictionaryReader reader = GetBufferedReaderAtBody())
            {
                if (Version == MessageVersion.None)
                {
                    writer.WriteNode(reader, false);
                }
                else
                {
                    if (!reader.IsEmptyElement)
                    {
                        reader.ReadStartElement();
                        while (reader.NodeType != XmlNodeType.EndElement)
                        {
                            writer.WriteNode(reader, false);
                        }
                    }
                }
            }
        }
        protected override void OnClose()
        {
            Exception ex = null;
            try
            {
                base.OnClose();
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }

                ex = e;
            }

            try
            {
                _properties.Dispose();
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }

                if (ex == null)
                {
                    ex = e;
                }
            }

            try
            {
                if (_reader != null)
                {
                    _reader.Dispose();
                }
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }

                if (ex == null)
                {
                    ex = e;
                }
            }

            try
            {
                _recycledMessageState.ReturnHeaders(_headers);
                _recycledMessageState.ReturnProperties(_properties);
                MessageData.ReturnMessageState(_recycledMessageState);
                _recycledMessageState = null;
                MessageData.Close();
                MessageData = null;
            }
            catch (Exception e)
            {
                if (Fx.IsFatal(e))
                {
                    throw;
                }

                if (ex == null)
                {
                    ex = e;
                }
            }

            if (ex != null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(ex);
            }
        }

        protected override void OnWriteStartEnvelope(XmlDictionaryWriter writer)
        {
            using (XmlDictionaryReader reader = GetMessageReader())
            {
                reader.MoveToContent();
                EnvelopeVersion envelopeVersion = Version.Envelope;
                writer.WriteStartElement(reader.Prefix, MessageStrings.Envelope, envelopeVersion.Namespace);
                writer.WriteAttributes(reader, false);
            }
        }

        protected override void OnWriteStartHeaders(XmlDictionaryWriter writer)
        {
            using (XmlDictionaryReader reader = GetMessageReader())
            {
                reader.MoveToContent();
                EnvelopeVersion envelopeVersion = Version.Envelope;
                reader.Read();
                if (HasHeaderElement(reader, envelopeVersion))
                {
                    writer.WriteStartElement(reader.Prefix, MessageStrings.Header, envelopeVersion.Namespace);
                    writer.WriteAttributes(reader, false);
                }
                else
                {
                    writer.WriteStartElement(MessageStrings.Prefix, MessageStrings.Header, envelopeVersion.Namespace);
                }
            }
        }

        protected override void OnWriteStartBody(XmlDictionaryWriter writer)
        {
            using (XmlDictionaryReader reader = GetBufferedReaderAtBody())
            {
                writer.WriteStartElement(reader.Prefix, MessageStrings.Body, Version.Envelope.Namespace);
                writer.WriteAttributes(reader, false);
            }
        }

        protected override MessageBuffer OnCreateBufferedCopy(int maxBufferSize)
        {
            if (_headers.ContainsOnlyBufferedMessageHeaders)
            {
                KeyValuePair<string, object>[] properties = new KeyValuePair<string, object>[Properties.Count];
                ((ICollection<KeyValuePair<string, object>>)Properties).CopyTo(properties, 0);
                MessageData.EnableMultipleUsers();
                bool[] understoodHeaders = null;
                if (_headers.UnderstoodHeaders.Modified)
                {
                    understoodHeaders = new bool[_headers.Count];
                    for (int i = 0; i < _headers.Count; i++)
                    {
                        understoodHeaders[i] = _headers.IsUnderstood(i);
                    }
                }
                return new BufferedMessageBuffer(MessageData, properties, understoodHeaders, _headers.UnderstoodHeaders.Modified);
            }
            else
            {
                if (_reader != null)
                {
                    return OnCreateBufferedCopy(maxBufferSize, _reader.Quotas);
                }

                return OnCreateBufferedCopy(maxBufferSize, XmlDictionaryReaderQuotas.Max);
            }
        }

        protected override string OnGetBodyAttribute(string localName, string ns)
        {
            if (_bodyAttributes != null)
            {
                return XmlAttributeHolder.GetAttribute(_bodyAttributes, localName, ns);
            }

            using (XmlDictionaryReader reader = GetBufferedReaderAtBody())
            {
                return reader.GetAttribute(localName, ns);
            }
        }
    }

    internal struct XmlAttributeHolder
    {
        public static XmlAttributeHolder[] emptyArray = Array.Empty<XmlAttributeHolder>();

        public XmlAttributeHolder(string prefix, string localName, string ns, string value)
        {
            Prefix = prefix;
            LocalName = localName;
            NamespaceUri = ns;
            Value = value;
        }

        public string Prefix { get; }

        public string NamespaceUri { get; }

        public string LocalName { get; }

        public string Value { get; }

        public void WriteTo(XmlWriter writer)
        {
            writer.WriteStartAttribute(Prefix, LocalName, NamespaceUri);
            writer.WriteString(Value);
            writer.WriteEndAttribute();
        }

        public static void WriteAttributes(XmlAttributeHolder[] attributes, XmlWriter writer)
        {
            for (int i = 0; i < attributes.Length; i++)
            {
                attributes[i].WriteTo(writer);
            }
        }

        public static XmlAttributeHolder[] ReadAttributes(XmlDictionaryReader reader)
        {
            int maxSizeOfHeaders = int.MaxValue;
            return ReadAttributes(reader, ref maxSizeOfHeaders);
        }

        public static XmlAttributeHolder[] ReadAttributes(XmlDictionaryReader reader, ref int maxSizeOfHeaders)
        {
            if (reader.AttributeCount == 0)
            {
                return emptyArray;
            }

            XmlAttributeHolder[] attributes = new XmlAttributeHolder[reader.AttributeCount];
            reader.MoveToFirstAttribute();
            for (int i = 0; i < attributes.Length; i++)
            {
                string ns = reader.NamespaceURI;
                string localName = reader.LocalName;
                string prefix = reader.Prefix;
                string value = string.Empty;
                while (reader.ReadAttributeValue())
                {
                    if (value.Length == 0)
                    {
                        value = reader.Value;
                    }
                    else
                    {
                        value += reader.Value;
                    }
                }
                Deduct(prefix, ref maxSizeOfHeaders);
                Deduct(localName, ref maxSizeOfHeaders);
                Deduct(ns, ref maxSizeOfHeaders);
                Deduct(value, ref maxSizeOfHeaders);
                attributes[i] = new XmlAttributeHolder(prefix, localName, ns, value);
                reader.MoveToNextAttribute();
            }
            reader.MoveToElement();
            return attributes;
        }

        private static void Deduct(string s, ref int maxSizeOfHeaders)
        {
            int byteCount = s.Length * sizeof(char);
            if (byteCount > maxSizeOfHeaders)
            {
                string message = SRCommon.XmlBufferQuotaExceeded;
                Exception inner = new QuotaExceededException(message);
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CommunicationException(message, inner));
            }
            maxSizeOfHeaders -= byteCount;
        }

        public static string GetAttribute(XmlAttributeHolder[] attributes, string localName, string ns)
        {
            for (int i = 0; i < attributes.Length; i++)
            {
                if (attributes[i].LocalName == localName && attributes[i].NamespaceUri == ns)
                {
                    return attributes[i].Value;
                }
            }

            return null;
        }
    }
}
