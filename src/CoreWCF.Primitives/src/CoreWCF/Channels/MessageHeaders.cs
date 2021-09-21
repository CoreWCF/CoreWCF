// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using System.Threading;
using System.Xml;
using CoreWCF.Dispatcher;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    public sealed class MessageHeaders : IEnumerable<MessageHeaderInfo>
    {
        private Header[] _headers;
        private IBufferedMessageData _bufferedMessageData;
        private UnderstoodHeaders _understoodHeaders;
        private const int InitialHeaderCount = 4;
        private const int MaxRecycledArrayLength = 8;
        private static XmlDictionaryString[] s_localNames;

        public const string WildcardAction = "*";

        // The highest node and attribute counts reached by the BVTs were 1829 and 667 respectively.
        private const int MaxBufferedHeaderNodes = 4096;
        private const int MaxBufferedHeaderAttributes = 2048;
        private int _nodeCount = 0;
        private int _attrCount = 0;
        private bool _understoodHeadersModified;

        public MessageHeaders(MessageVersion version, int initialSize)
        {
            Init(version, initialSize);
        }

        public MessageHeaders(MessageVersion version)
            : this(version, InitialHeaderCount)
        {
        }

        internal MessageHeaders(MessageVersion version, XmlDictionaryReader reader, XmlAttributeHolder[] envelopeAttributes, XmlAttributeHolder[] headerAttributes, ref int maxSizeOfHeaders)
            : this(version)
        {
            if (maxSizeOfHeaders < 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new ArgumentOutOfRangeException(nameof(maxSizeOfHeaders), maxSizeOfHeaders,
                    SR.ValueMustBeNonNegative));
            }

            if (version == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(version));
            }

            if (reader == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(reader));
            }

            if (reader.IsEmptyElement)
            {
                reader.Read();
                return;
            }
            XmlBuffer xmlBuffer = null;
            EnvelopeVersion envelopeVersion = version.Envelope;
            reader.ReadStartElement(XD.MessageDictionary.Header, envelopeVersion.DictionaryNamespace);
            while (reader.IsStartElement())
            {
                if (xmlBuffer == null)
                {
                    xmlBuffer = new XmlBuffer(maxSizeOfHeaders);
                }

                BufferedHeader bufferedHeader = new BufferedHeader(version, xmlBuffer, reader, envelopeAttributes, headerAttributes);
                HeaderProcessing processing = bufferedHeader.MustUnderstand ? HeaderProcessing.MustUnderstand : 0;
                HeaderKind kind = GetHeaderKind(bufferedHeader);
                if (kind != HeaderKind.Unknown)
                {
                    processing |= HeaderProcessing.Understood;
                    TraceUnderstood(bufferedHeader);
                }
                Header newHeader = new Header(kind, bufferedHeader, processing);
                AddHeader(newHeader);
            }
            if (xmlBuffer != null)
            {
                xmlBuffer.Close();
                maxSizeOfHeaders -= xmlBuffer.BufferSize;
            }
            reader.ReadEndElement();
            CollectionVersion = 0;
        }

        internal MessageHeaders(MessageVersion version, XmlDictionaryReader reader, IBufferedMessageData bufferedMessageData, RecycledMessageState recycledMessageState, bool[] understoodHeaders, bool understoodHeadersModified)
        {
            _headers = new Header[InitialHeaderCount];
            Init(version, reader, bufferedMessageData, recycledMessageState, understoodHeaders, understoodHeadersModified);
        }

        internal MessageHeaders(MessageVersion version, MessageHeaders headers, IBufferedMessageData bufferedMessageData)
        {
            MessageVersion = version;
            _bufferedMessageData = bufferedMessageData;
            Count = headers.Count;
            _headers = new Header[Count];
            Array.Copy(headers._headers, _headers, Count);
            CollectionVersion = 0;
        }

        public MessageHeaders(MessageHeaders collection)
        {
            if (collection == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(collection));
            }

            Init(collection.MessageVersion, collection._headers.Length);
            CopyHeadersFrom(collection);
            CollectionVersion = 0;
        }

        public string Action
        {
            get
            {
                int index = FindHeaderProperty(HeaderKind.Action);
                if (index < 0)
                {
                    return null;
                }

                if (_headers[index].HeaderInfo is ActionHeader actionHeader)
                {
                    return actionHeader.Action;
                }

                using (XmlDictionaryReader reader = GetReaderAtHeader(index))
                {
                    return ActionHeader.ReadHeaderValue(reader, MessageVersion.Addressing);
                }
            }
            set
            {
                if (value != null)
                {
                    SetActionHeader(ActionHeader.Create(value, MessageVersion.Addressing));
                }
                else
                {
                    SetHeaderProperty(HeaderKind.Action, null);
                }
            }
        }

        internal bool CanRecycle
        {
            get { return _headers.Length <= MaxRecycledArrayLength; }
        }

        internal bool ContainsOnlyBufferedMessageHeaders
        {
            get { return (_bufferedMessageData != null && CollectionVersion == 0); }
        }

        internal int CollectionVersion { get; private set; }

        public int Count { get; private set; }

        public EndpointAddress FaultTo
        {
            get
            {
                int index = FindHeaderProperty(HeaderKind.FaultTo);
                if (index < 0)
                {
                    return null;
                }

                if (_headers[index].HeaderInfo is FaultToHeader faultToHeader)
                {
                    return faultToHeader.FaultTo;
                }

                using (XmlDictionaryReader reader = GetReaderAtHeader(index))
                {
                    return FaultToHeader.ReadHeaderValue(reader, MessageVersion.Addressing);
                }
            }
            set
            {
                if (value != null)
                {
                    SetFaultToHeader(FaultToHeader.Create(value, MessageVersion.Addressing));
                }
                else
                {
                    SetHeaderProperty(HeaderKind.FaultTo, null);
                }
            }
        }

        public EndpointAddress From
        {
            get
            {
                int index = FindHeaderProperty(HeaderKind.From);
                if (index < 0)
                {
                    return null;
                }

                if (_headers[index].HeaderInfo is FromHeader fromHeader)
                {
                    return fromHeader.From;
                }

                using (XmlDictionaryReader reader = GetReaderAtHeader(index))
                {
                    return FromHeader.ReadHeaderValue(reader, MessageVersion.Addressing);
                }
            }
            set
            {
                if (value != null)
                {
                    SetFromHeader(FromHeader.Create(value, MessageVersion.Addressing));
                }
                else
                {
                    SetHeaderProperty(HeaderKind.From, null);
                }
            }
        }

        internal bool HasMustUnderstandBeenModified
        {
            get
            {
                if (_understoodHeaders != null)
                {
                    return _understoodHeaders.Modified;
                }
                else
                {
                    return _understoodHeadersModified;
                }
            }
        }

        public UniqueId MessageId
        {
            get
            {
                int index = FindHeaderProperty(HeaderKind.MessageId);
                if (index < 0)
                {
                    return null;
                }

                if (_headers[index].HeaderInfo is MessageIDHeader messageIDHeader)
                {
                    return messageIDHeader.MessageId;
                }

                using (XmlDictionaryReader reader = GetReaderAtHeader(index))
                {
                    return MessageIDHeader.ReadHeaderValue(reader, MessageVersion.Addressing);
                }
            }
            set
            {
                if (value != null)
                {
                    SetMessageIDHeader(MessageIDHeader.Create(value, MessageVersion.Addressing));
                }
                else
                {
                    SetHeaderProperty(HeaderKind.MessageId, null);
                }
            }
        }

        public MessageVersion MessageVersion { get; private set; }

        public UniqueId RelatesTo
        {
            get
            {
                return GetRelatesTo(RelatesToHeader.ReplyRelationshipType);
            }
            set
            {
                SetRelatesTo(RelatesToHeader.ReplyRelationshipType, value);
            }
        }

        public EndpointAddress ReplyTo
        {
            get
            {
                int index = FindHeaderProperty(HeaderKind.ReplyTo);
                if (index < 0)
                {
                    return null;
                }

                if (_headers[index].HeaderInfo is ReplyToHeader replyToHeader)
                {
                    return replyToHeader.ReplyTo;
                }

                using (XmlDictionaryReader reader = GetReaderAtHeader(index))
                {
                    return ReplyToHeader.ReadHeaderValue(reader, MessageVersion.Addressing);
                }
            }
            set
            {
                if (value != null)
                {
                    SetReplyToHeader(ReplyToHeader.Create(value, MessageVersion.Addressing));
                }
                else
                {
                    SetHeaderProperty(HeaderKind.ReplyTo, null);
                }
            }
        }

        public Uri To
        {
            get
            {
                int index = FindHeaderProperty(HeaderKind.To);
                if (index < 0)
                {
                    return null;
                }

                if (_headers[index].HeaderInfo is ToHeader toHeader)
                {
                    return toHeader.To;
                }

                using (XmlDictionaryReader reader = GetReaderAtHeader(index))
                {
                    return ToHeader.ReadHeaderValue(reader, MessageVersion.Addressing);
                }
            }
            set
            {
                if (value != null)
                {
                    SetToHeader(ToHeader.Create(value, MessageVersion.Addressing));
                }
                else
                {
                    SetHeaderProperty(HeaderKind.To, null);
                }
            }
        }

        internal UnderstoodHeaders UnderstoodHeaders
        {
            get
            {
                if (_understoodHeaders == null)
                {
                    _understoodHeaders = new UnderstoodHeaders(this, _understoodHeadersModified);
                }

                return _understoodHeaders;
            }
        }

        public MessageHeaderInfo this[int index]
        {
            get
            {
                if (index < 0 || index >= Count)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                        new ArgumentOutOfRangeException(nameof(index), index,
                        SR.Format(SR.ValueMustBeInRange, 0, Count)));
                }

                return _headers[index].HeaderInfo;
            }
        }

        public void Add(MessageHeader header)
        {
            Insert(Count, header);
        }

        internal void AddActionHeader(ActionHeader actionHeader)
        {
            Insert(Count, actionHeader, HeaderKind.Action);
        }

        internal void AddMessageIDHeader(MessageIDHeader messageIDHeader)
        {
            Insert(Count, messageIDHeader, HeaderKind.MessageId);
        }

        internal void AddRelatesToHeader(RelatesToHeader relatesToHeader)
        {
            Insert(Count, relatesToHeader, HeaderKind.RelatesTo);
        }

        internal void AddReplyToHeader(ReplyToHeader replyToHeader)
        {
            Insert(Count, replyToHeader, HeaderKind.ReplyTo);
        }

        internal void AddToHeader(ToHeader toHeader)
        {
            Insert(Count, toHeader, HeaderKind.To);
        }

        private void Add(MessageHeader header, HeaderKind kind)
        {
            Insert(Count, header, kind);
        }

        private void AddHeader(Header header)
        {
            InsertHeader(Count, header);
        }

        internal void AddUnderstood(int i)
        {
            _headers[i].HeaderProcessing |= HeaderProcessing.Understood;
            TraceUnderstood(_headers[i].HeaderInfo);
        }

        internal void AddUnderstood(MessageHeaderInfo headerInfo)
        {
            if (headerInfo == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(headerInfo));
            }

            for (int i = 0; i < Count; i++)
            {
                if ((object)_headers[i].HeaderInfo == (object)headerInfo)
                {
                    if ((_headers[i].HeaderProcessing & HeaderProcessing.Understood) != 0)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(
                            SR.Format(SR.HeaderAlreadyUnderstood, headerInfo.Name, headerInfo.Namespace), nameof(headerInfo)));
                    }

                    AddUnderstood(i);
                }
            }
        }

        private void CaptureBufferedHeaders()
        {
            CaptureBufferedHeaders(-1);
        }

        private void CaptureBufferedHeaders(int exceptIndex)
        {
            using (XmlDictionaryReader reader = GetBufferedMessageHeaderReaderAtHeaderContents(_bufferedMessageData))
            {
                for (int i = 0; i < Count; i++)
                {
                    if (reader.NodeType != XmlNodeType.Element)
                    {
                        if (reader.MoveToContent() != XmlNodeType.Element)
                        {
                            break;
                        }
                    }

                    Header header = _headers[i];
                    if (i == exceptIndex || header.HeaderType != HeaderType.BufferedMessageHeader)
                    {
                        reader.Skip();
                    }
                    else
                    {
                        _headers[i] = new Header(header.HeaderKind, CaptureBufferedHeader(reader,
                            header.HeaderInfo), header.HeaderProcessing);
                    }
                }
            }
            _bufferedMessageData = null;
        }

        private BufferedHeader CaptureBufferedHeader(XmlDictionaryReader reader, MessageHeaderInfo headerInfo)
        {
            XmlBuffer buffer = new XmlBuffer(int.MaxValue);
            XmlDictionaryWriter writer = buffer.OpenSection(_bufferedMessageData.Quotas);
            writer.WriteNode(reader, false);
            buffer.CloseSection();
            buffer.Close();
            return new BufferedHeader(MessageVersion, buffer, 0, headerInfo);
        }

        private BufferedHeader CaptureBufferedHeader(IBufferedMessageData bufferedMessageData, MessageHeaderInfo headerInfo, int bufferedMessageHeaderIndex)
        {
            XmlBuffer buffer = new XmlBuffer(int.MaxValue);
            XmlDictionaryWriter writer = buffer.OpenSection(bufferedMessageData.Quotas);
            WriteBufferedMessageHeader(bufferedMessageData, bufferedMessageHeaderIndex, writer);
            buffer.CloseSection();
            buffer.Close();
            return new BufferedHeader(MessageVersion, buffer, 0, headerInfo);
        }

        private BufferedHeader CaptureWriteableHeader(MessageHeader writeableHeader)
        {
            XmlBuffer buffer = new XmlBuffer(int.MaxValue);
            XmlDictionaryWriter writer = buffer.OpenSection(XmlDictionaryReaderQuotas.Max);
            writeableHeader.WriteHeader(writer, MessageVersion);
            buffer.CloseSection();
            buffer.Close();
            return new BufferedHeader(MessageVersion, buffer, 0, writeableHeader);
        }

        public void Clear()
        {
            for (int i = 0; i < Count; i++)
            {
                _headers[i] = new Header();
            }

            Count = 0;
            CollectionVersion++;
            _bufferedMessageData = null;
        }

        public void CopyHeaderFrom(Message message, int headerIndex)
        {
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));
            }

            CopyHeaderFrom(message.Headers, headerIndex);
        }

        public void CopyHeaderFrom(MessageHeaders collection, int headerIndex)
        {
            if (collection == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(collection));
            }

            if (collection.MessageVersion != MessageVersion)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.Format(SR.MessageHeaderVersionMismatch, collection.MessageVersion.ToString(), MessageVersion.ToString()), nameof(collection)));
            }

            if (headerIndex < 0 || headerIndex >= collection.Count)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new ArgumentOutOfRangeException(nameof(headerIndex), headerIndex,
                    SR.Format(SR.ValueMustBeInRange, 0, collection.Count)));
            }
            Header header = collection._headers[headerIndex];
            HeaderProcessing processing = header.HeaderInfo.MustUnderstand ? HeaderProcessing.MustUnderstand : 0;
            if ((header.HeaderProcessing & HeaderProcessing.Understood) != 0 || header.HeaderKind != HeaderKind.Unknown)
            {
                processing |= HeaderProcessing.Understood;
            }

            switch (header.HeaderType)
            {
                case HeaderType.BufferedMessageHeader:
                    AddHeader(new Header(header.HeaderKind, collection.CaptureBufferedHeader(collection._bufferedMessageData,
                        header.HeaderInfo, headerIndex), processing));
                    break;
                case HeaderType.ReadableHeader:
                    AddHeader(new Header(header.HeaderKind, header.ReadableHeader, processing));
                    break;
                case HeaderType.WriteableHeader:
                    AddHeader(new Header(header.HeaderKind, header.MessageHeader, processing));
                    break;
                default:
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.InvalidEnumValue, header.HeaderType)));
            }
        }

        public void CopyHeadersFrom(Message message)
        {
            if (message == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));
            }

            CopyHeadersFrom(message.Headers);
        }

        public void CopyHeadersFrom(MessageHeaders collection)
        {
            if (collection == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(collection));
            }

            for (int i = 0; i < collection.Count; i++)
            {
                CopyHeaderFrom(collection, i);
            }
        }

        public void CopyTo(MessageHeaderInfo[] array, int index)
        {
            if (array == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(array));
            }

            if (index < 0 || (index + Count) > array.Length)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new ArgumentOutOfRangeException(nameof(index), index,
                    SR.Format(SR.ValueMustBeInRange, 0, array.Length - Count)));
            }
            for (int i = 0; i < Count; i++)
            {
                array[i + index] = _headers[i].HeaderInfo;
            }
        }

        private Exception CreateDuplicateHeaderException(HeaderKind kind)
        {
            string name;
            switch (kind)
            {
                case HeaderKind.Action:
                    name = AddressingStrings.Action;
                    break;
                case HeaderKind.FaultTo:
                    name = AddressingStrings.FaultTo;
                    break;
                case HeaderKind.From:
                    name = AddressingStrings.From;
                    break;
                case HeaderKind.MessageId:
                    name = AddressingStrings.MessageId;
                    break;
                case HeaderKind.ReplyTo:
                    name = AddressingStrings.ReplyTo;
                    break;
                case HeaderKind.To:
                    name = AddressingStrings.To;
                    break;
                default:
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.InvalidEnumValue, kind)));
            }

            return new MessageHeaderException(
                SR.Format(SR.MultipleMessageHeaders, name, MessageVersion.Addressing.Namespace),
                name,
                MessageVersion.Addressing.Namespace,
                true);
        }

        public int FindHeader(string name, string ns)
        {
            if (name == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(name));
            }

            if (ns == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(ns));
            }

            if (ns == MessageVersion.Addressing.Namespace)
            {
                return FindAddressingHeader(name, ns);
            }
            else
            {
                return FindNonAddressingHeader(name, ns, MessageVersion.Envelope.UltimateDestinationActorValues);
            }
        }

        private int FindAddressingHeader(string name, string ns)
        {
            int foundAt = -1;
            for (int i = 0; i < Count; i++)
            {
                if (_headers[i].HeaderKind != HeaderKind.Unknown)
                {
                    MessageHeaderInfo info = _headers[i].HeaderInfo;
                    if (info.Name == name && info.Namespace == ns)
                    {
                        if (foundAt >= 0)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                                new MessageHeaderException(SR.Format(SR.MultipleMessageHeaders, name, ns), name, ns, true));
                        }
                        foundAt = i;
                    }
                }
            }
            return foundAt;
        }

        private int FindNonAddressingHeader(string name, string ns, string[] actors)
        {
            int foundAt = -1;
            for (int i = 0; i < Count; i++)
            {
                if (_headers[i].HeaderKind == HeaderKind.Unknown)
                {
                    MessageHeaderInfo info = _headers[i].HeaderInfo;
                    if (info.Name == name && info.Namespace == ns)
                    {
                        for (int j = 0; j < actors.Length; j++)
                        {
                            if (actors[j] == info.Actor)
                            {
                                if (foundAt >= 0)
                                {
                                    if (actors.Length == 1)
                                    {
                                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageHeaderException(SR.Format(SR.MultipleMessageHeadersWithActor, name, ns, actors[0]), name, ns, true));
                                    }

                                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageHeaderException(SR.Format(SR.MultipleMessageHeaders, name, ns), name, ns, true));
                                }
                                foundAt = i;
                            }
                        }
                    }
                }
            }
            return foundAt;
        }

        public int FindHeader(string name, string ns, params string[] actors)
        {
            if (name == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(name));
            }

            if (ns == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(ns));
            }

            if (actors == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(actors));
            }

            int foundAt = -1;
            for (int i = 0; i < Count; i++)
            {
                MessageHeaderInfo info = _headers[i].HeaderInfo;
                if (info.Name == name && info.Namespace == ns)
                {
                    for (int j = 0; j < actors.Length; j++)
                    {
                        if (actors[j] == info.Actor)
                        {
                            if (foundAt >= 0)
                            {
                                if (actors.Length == 1)
                                {
                                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageHeaderException(SR.Format(SR.MultipleMessageHeadersWithActor, name, ns, actors[0]), name, ns, true));
                                }

                                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageHeaderException(SR.Format(SR.MultipleMessageHeaders, name, ns), name, ns, true));
                            }
                            foundAt = i;
                        }
                    }
                }
            }
            return foundAt;
        }

        private int FindHeaderProperty(HeaderKind kind)
        {
            int index = -1;
            for (int i = 0; i < Count; i++)
            {
                if (_headers[i].HeaderKind == kind)
                {
                    if (index >= 0)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateDuplicateHeaderException(kind));
                    }

                    index = i;
                }
            }
            return index;
        }

        private int FindRelatesTo(Uri relationshipType, out UniqueId messageId)
        {
            UniqueId foundValue = null;
            int foundIndex = -1;
            for (int i = 0; i < Count; i++)
            {
                if (_headers[i].HeaderKind == HeaderKind.RelatesTo)
                {
                    GetRelatesToValues(i, out Uri tempRelationship, out UniqueId tempValue);

                    if (relationshipType == tempRelationship)
                    {
                        if (foundValue != null)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                                new MessageHeaderException(
                                    SR.Format(SR.MultipleRelatesToHeaders, relationshipType.AbsoluteUri),
                                    AddressingStrings.RelatesTo,
                                    MessageVersion.Addressing.Namespace,
                                    true));
                        }
                        foundValue = tempValue;
                        foundIndex = i;
                    }
                }
            }

            messageId = foundValue;
            return foundIndex;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<MessageHeaderInfo> GetEnumerator()
        {
            MessageHeaderInfo[] headers = new MessageHeaderInfo[Count];
            CopyTo(headers, 0);
            return GetEnumerator(headers);
        }

        private IEnumerator<MessageHeaderInfo> GetEnumerator(MessageHeaderInfo[] headers)
        {
            IList<MessageHeaderInfo> list = new ReadOnlyCollection<MessageHeaderInfo>(headers);
            return list.GetEnumerator();
        }

        internal IEnumerator<MessageHeaderInfo> GetUnderstoodEnumerator()
        {
            List<MessageHeaderInfo> understoodHeaders = new List<MessageHeaderInfo>();

            for (int i = 0; i < Count; i++)
            {
                if ((_headers[i].HeaderProcessing & HeaderProcessing.Understood) != 0)
                {
                    understoodHeaders.Add(_headers[i].HeaderInfo);
                }
            }

            return understoodHeaders.GetEnumerator();
        }

        private static XmlDictionaryReader GetBufferedMessageHeaderReaderAtHeaderContents(IBufferedMessageData bufferedMessageData)
        {
            XmlDictionaryReader reader = bufferedMessageData.GetMessageReader();
            if (reader.NodeType == XmlNodeType.Element)
            {
                reader.Read();
            }
            else
            {
                reader.ReadStartElement();
            }

            if (reader.NodeType == XmlNodeType.Element)
            {
                reader.Read();
            }
            else
            {
                reader.ReadStartElement();
            }

            return reader;
        }

        private XmlDictionaryReader GetBufferedMessageHeaderReader(IBufferedMessageData bufferedMessageData, int bufferedMessageHeaderIndex)
        {
            // Check if we need to change representations
            if (_nodeCount > MaxBufferedHeaderNodes || _attrCount > MaxBufferedHeaderAttributes)
            {
                CaptureBufferedHeaders();
                return _headers[bufferedMessageHeaderIndex].ReadableHeader.GetHeaderReader();
            }

            XmlDictionaryReader reader = GetBufferedMessageHeaderReaderAtHeaderContents(bufferedMessageData);
            for (; ; )
            {
                if (reader.NodeType != XmlNodeType.Element)
                {
                    reader.MoveToContent();
                }

                if (bufferedMessageHeaderIndex == 0)
                {
                    break;
                }

                Skip(reader);
                bufferedMessageHeaderIndex--;
            }

            return reader;
        }

        private void Skip(XmlDictionaryReader reader)
        {
            if (reader.MoveToContent() == XmlNodeType.Element && !reader.IsEmptyElement)
            {
                int depth = reader.Depth;
                do
                {
                    _attrCount += reader.AttributeCount;
                    _nodeCount++;
                } while (reader.Read() && depth < reader.Depth);

                // consume end tag
                if (reader.NodeType == XmlNodeType.EndElement)
                {
                    _nodeCount++;
                    reader.Read();
                }
            }
            else
            {
                _attrCount += reader.AttributeCount;
                _nodeCount++;
                reader.Read();
            }
        }

        public T GetHeader<T>(string name, string ns)
        {
            return GetHeader<T>(name, ns, DataContractSerializerDefaults.CreateSerializer(typeof(T), name, ns, int.MaxValue/*maxItems*/));
        }

        public T GetHeader<T>(string name, string ns, params string[] actors)
        {
            int index = FindHeader(name, ns, actors);
            if (index < 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageHeaderException(SR.Format(SR.HeaderNotFound, name, ns), name, ns));
            }

            return GetHeader<T>(index);
        }

        public T GetHeader<T>(string name, string ns, XmlObjectSerializer serializer)
        {
            if (serializer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(serializer));
            }

            int index = FindHeader(name, ns);
            if (index < 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageHeaderException(SR.Format(SR.HeaderNotFound, name, ns), name, ns));
            }

            return GetHeader<T>(index, serializer);
        }

        public T GetHeader<T>(int index)
        {
            if (index < 0 || index >= Count)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new ArgumentOutOfRangeException(nameof(index), index,
                    SR.Format(SR.ValueMustBeInRange, 0, Count)));
            }

            MessageHeaderInfo headerInfo = _headers[index].HeaderInfo;
            return GetHeader<T>(index, DataContractSerializerDefaults.CreateSerializer(typeof(T), headerInfo.Name, headerInfo.Namespace, int.MaxValue/*maxItems*/));
        }

        public T GetHeader<T>(int index, XmlObjectSerializer serializer)
        {
            if (serializer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(serializer));
            }

            using (XmlDictionaryReader reader = GetReaderAtHeader(index))
            {
                return (T)serializer.ReadObject(reader);
            }
        }

        private HeaderKind GetHeaderKind(MessageHeaderInfo headerInfo)
        {
            HeaderKind headerKind = HeaderKind.Unknown;

            if (headerInfo.Namespace == MessageVersion.Addressing.Namespace)
            {
                if (MessageVersion.Envelope.IsUltimateDestinationActor(headerInfo.Actor))
                {
                    string name = headerInfo.Name;
                    if (name.Length > 0)
                    {
                        switch (name[0])
                        {
                            case 'A':
                                if (name == AddressingStrings.Action)
                                {
                                    headerKind = HeaderKind.Action;
                                }
                                break;
                            case 'F':
                                if (name == AddressingStrings.From)
                                {
                                    headerKind = HeaderKind.From;
                                }
                                else if (name == AddressingStrings.FaultTo)
                                {
                                    headerKind = HeaderKind.FaultTo;
                                }
                                break;
                            case 'M':
                                if (name == AddressingStrings.MessageId)
                                {
                                    headerKind = HeaderKind.MessageId;
                                }
                                break;
                            case 'R':
                                if (name == AddressingStrings.ReplyTo)
                                {
                                    headerKind = HeaderKind.ReplyTo;
                                }
                                else if (name == AddressingStrings.RelatesTo)
                                {
                                    headerKind = HeaderKind.RelatesTo;
                                }
                                break;
                            case 'T':
                                if (name == AddressingStrings.To)
                                {
                                    headerKind = HeaderKind.To;
                                }
                                break;
                        }
                    }
                }
            }

            ValidateHeaderKind(headerKind);
            return headerKind;
        }

        private void ValidateHeaderKind(HeaderKind headerKind)
        {
            if (MessageVersion.Envelope == EnvelopeVersion.None)
            {
                if (headerKind != HeaderKind.Action && headerKind != HeaderKind.To)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                        new InvalidOperationException(SR.Format(SR.HeadersCannotBeAddedToEnvelopeVersion, MessageVersion.Envelope)));
                }
            }

            if (MessageVersion.Addressing == AddressingVersion.None)
            {
                if (headerKind != HeaderKind.Unknown && headerKind != HeaderKind.Action && headerKind != HeaderKind.To)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                        new InvalidOperationException(SR.Format(SR.AddressingHeadersCannotBeAddedToAddressingVersion, MessageVersion.Addressing)));
                }
            }
        }

        public XmlDictionaryReader GetReaderAtHeader(int headerIndex)
        {
            if (headerIndex < 0 || headerIndex >= Count)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new ArgumentOutOfRangeException(nameof(headerIndex), headerIndex,
                    SR.Format(SR.ValueMustBeInRange, 0, Count)));
            }

            switch (_headers[headerIndex].HeaderType)
            {
                case HeaderType.ReadableHeader:
                    return _headers[headerIndex].ReadableHeader.GetHeaderReader();
                case HeaderType.WriteableHeader:
                    MessageHeader writeableHeader = _headers[headerIndex].MessageHeader;
                    BufferedHeader bufferedHeader = CaptureWriteableHeader(writeableHeader);
                    _headers[headerIndex] = new Header(_headers[headerIndex].HeaderKind, bufferedHeader, _headers[headerIndex].HeaderProcessing);
                    CollectionVersion++;
                    return bufferedHeader.GetHeaderReader();
                case HeaderType.BufferedMessageHeader:
                    return GetBufferedMessageHeaderReader(_bufferedMessageData, headerIndex);
                default:
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.InvalidEnumValue, _headers[headerIndex].HeaderType)));
            }
        }

        internal UniqueId GetRelatesTo(Uri relationshipType)
        {
            if (relationshipType == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(relationshipType));
            }

            FindRelatesTo(relationshipType, out UniqueId messageId);
            return messageId;
        }

        private void GetRelatesToValues(int index, out Uri relationshipType, out UniqueId messageId)
        {
            if (_headers[index].HeaderInfo is RelatesToHeader relatesToHeader)
            {
                relationshipType = relatesToHeader.RelationshipType;
                messageId = relatesToHeader.UniqueId;
            }
            else
            {
                using (XmlDictionaryReader reader = GetReaderAtHeader(index))
                {
                    RelatesToHeader.ReadHeaderValue(reader, MessageVersion.Addressing, out relationshipType, out messageId);
                }
            }
        }

        internal string[] GetHeaderAttributes(string localName, string ns)
        {
            string[] attrs = null;

            if (ContainsOnlyBufferedMessageHeaders)
            {
                XmlDictionaryReader reader = _bufferedMessageData.GetMessageReader();
                reader.ReadStartElement(); // Envelope
                reader.ReadStartElement(); // Header
                for (int index = 0; reader.IsStartElement(); index++)
                {
                    string value = reader.GetAttribute(localName, ns);
                    if (value != null)
                    {
                        if (attrs == null)
                        {
                            attrs = new string[Count];
                        }

                        attrs[index] = value;
                    }
                    if (index == Count - 1)
                    {
                        break;
                    }

                    reader.Skip();
                }
                reader.Dispose();
            }
            else
            {
                for (int index = 0; index < Count; index++)
                {
                    if (_headers[index].HeaderType != HeaderType.WriteableHeader)
                    {
                        using (XmlDictionaryReader reader = GetReaderAtHeader(index))
                        {
                            string value = reader.GetAttribute(localName, ns);
                            if (value != null)
                            {
                                if (attrs == null)
                                {
                                    attrs = new string[Count];
                                }

                                attrs[index] = value;
                            }
                        }
                    }
                }
            }

            return attrs;
        }

        internal MessageHeader GetMessageHeader(int index)
        {
            if (index < 0 || index >= Count)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new ArgumentOutOfRangeException("headerIndex", index,
                    SR.Format(SR.ValueMustBeInRange, 0, Count)));
            }
            MessageHeader messageHeader;
            switch (_headers[index].HeaderType)
            {
                case HeaderType.WriteableHeader:
                case HeaderType.ReadableHeader:
                    return _headers[index].MessageHeader;
                case HeaderType.BufferedMessageHeader:
                    messageHeader = CaptureBufferedHeader(_bufferedMessageData, _headers[index].HeaderInfo, index);
                    _headers[index] = new Header(_headers[index].HeaderKind, messageHeader, _headers[index].HeaderProcessing);
                    CollectionVersion++;
                    return messageHeader;
                default:
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.InvalidEnumValue, _headers[index].HeaderType)));
            }
        }

        internal Collection<MessageHeaderInfo> GetHeadersNotUnderstood()
        {
            Collection<MessageHeaderInfo> notUnderstoodHeaders = null;

            for (int headerIndex = 0; headerIndex < Count; headerIndex++)
            {
                if (_headers[headerIndex].HeaderProcessing == HeaderProcessing.MustUnderstand)
                {
                    if (notUnderstoodHeaders == null)
                    {
                        notUnderstoodHeaders = new Collection<MessageHeaderInfo>();
                    }

                    MessageHeaderInfo headerInfo = _headers[headerIndex].HeaderInfo;
                    //if (DiagnosticUtility.ShouldTraceWarning)
                    //{
                    //    TraceUtility.TraceEvent(TraceEventType.Warning, TraceCode.DidNotUnderstandMessageHeader,
                    //        SR.TraceCodeDidNotUnderstandMessageHeader,
                    //        new MessageHeaderInfoTraceRecord(headerInfo), null, null);
                    //}

                    notUnderstoodHeaders.Add(headerInfo);
                }
            }

            return notUnderstoodHeaders;
        }

        public bool HaveMandatoryHeadersBeenUnderstood()
        {
            return HaveMandatoryHeadersBeenUnderstood(MessageVersion.Envelope.MustUnderstandActorValues);
        }

        public bool HaveMandatoryHeadersBeenUnderstood(params string[] actors)
        {
            if (actors == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(actors));
            }

            for (int headerIndex = 0; headerIndex < Count; headerIndex++)
            {
                if (_headers[headerIndex].HeaderProcessing == HeaderProcessing.MustUnderstand)
                {
                    for (int actorIndex = 0; actorIndex < actors.Length; ++actorIndex)
                    {
                        if (_headers[headerIndex].HeaderInfo.Actor == actors[actorIndex])
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        internal void Init(MessageVersion version, int initialSize)
        {
            _nodeCount = 0;
            _attrCount = 0;
            if (initialSize < 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new ArgumentOutOfRangeException(nameof(initialSize), initialSize,
                    SR.ValueMustBeNonNegative));
            }

            MessageVersion = version ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(version));
            _headers = new Header[initialSize];
        }

        internal void Init(MessageVersion version)
        {
            _nodeCount = 0;
            _attrCount = 0;
            MessageVersion = version;
            CollectionVersion = 0;
        }

        internal void Init(MessageVersion version, XmlDictionaryReader reader, IBufferedMessageData bufferedMessageData, RecycledMessageState recycledMessageState, bool[] understoodHeaders, bool understoodHeadersModified)
        {
            _nodeCount = 0;
            _attrCount = 0;
            MessageVersion = version;
            _bufferedMessageData = bufferedMessageData;

            if (version.Envelope != EnvelopeVersion.None)
            {
                _understoodHeadersModified = (understoodHeaders != null) && understoodHeadersModified;
                if (reader.IsEmptyElement)
                {
                    reader.Read();
                    return;
                }
                EnvelopeVersion envelopeVersion = version.Envelope;
                Fx.Assert(reader.IsStartElement(XD.MessageDictionary.Header, envelopeVersion.DictionaryNamespace), "");
                reader.ReadStartElement();

                AddressingDictionary dictionary = XD.AddressingDictionary;

                if (s_localNames == null)
                {
                    XmlDictionaryString[] strings = new XmlDictionaryString[7];
                    strings[(int)HeaderKind.To] = dictionary.To;
                    strings[(int)HeaderKind.Action] = dictionary.Action;
                    strings[(int)HeaderKind.MessageId] = dictionary.MessageId;
                    strings[(int)HeaderKind.RelatesTo] = dictionary.RelatesTo;
                    strings[(int)HeaderKind.ReplyTo] = dictionary.ReplyTo;
                    strings[(int)HeaderKind.From] = dictionary.From;
                    strings[(int)HeaderKind.FaultTo] = dictionary.FaultTo;
                    Interlocked.MemoryBarrier();
                    s_localNames = strings;
                }


                int i = 0;
                while (reader.IsStartElement())
                {
                    ReadBufferedHeader(reader, recycledMessageState, s_localNames, (understoodHeaders == null) ? false : understoodHeaders[i++]);
                }

                reader.ReadEndElement();
            }
            CollectionVersion = 0;
        }

        public void Insert(int headerIndex, MessageHeader header)
        {
            if (header == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(header));
            }

            if (!header.IsMessageVersionSupported(MessageVersion))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.Format(SR.MessageHeaderVersionNotSupported, header.GetType().FullName, MessageVersion.Envelope.ToString()), nameof(header)));
            }

            Insert(headerIndex, header, GetHeaderKind(header));
        }

        private void Insert(int headerIndex, MessageHeader header, HeaderKind kind)
        {
            HeaderProcessing processing = header.MustUnderstand ? HeaderProcessing.MustUnderstand : 0;
            if (kind != HeaderKind.Unknown)
            {
                processing |= HeaderProcessing.Understood;
            }

            if (header is ReadableMessageHeader readableMessageHeader)
            {
                InsertHeader(headerIndex, new Header(kind, readableMessageHeader, processing));
            }
            else
            {
                InsertHeader(headerIndex, new Header(kind, header, processing));
            }
        }

        private void InsertHeader(int headerIndex, Header header)
        {
            ValidateHeaderKind(header.HeaderKind);

            if (headerIndex < 0 || headerIndex > Count)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new ArgumentOutOfRangeException(nameof(headerIndex), headerIndex,
                    SR.Format(SR.ValueMustBeInRange, 0, Count)));
            }

            if (Count == _headers.Length)
            {
                if (_headers.Length == 0)
                {
                    _headers = new Header[1];
                }
                else
                {
                    Header[] newHeaders = new Header[_headers.Length * 2];
                    _headers.CopyTo(newHeaders, 0);
                    _headers = newHeaders;
                }
            }
            if (headerIndex < Count)
            {
                if (_bufferedMessageData != null)
                {
                    for (int i = headerIndex; i < Count; i++)
                    {
                        if (_headers[i].HeaderType == HeaderType.BufferedMessageHeader)
                        {
                            CaptureBufferedHeaders();
                            break;
                        }
                    }
                }
                Array.Copy(_headers, headerIndex, _headers, headerIndex + 1, Count - headerIndex);
            }
            _headers[headerIndex] = header;
            Count++;
            CollectionVersion++;
        }

        internal bool IsUnderstood(int i)
        {
            return (_headers[i].HeaderProcessing & HeaderProcessing.Understood) != 0;
        }

        internal bool IsUnderstood(MessageHeaderInfo headerInfo)
        {
            if (headerInfo == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(headerInfo));
            }

            for (int i = 0; i < Count; i++)
            {
                if ((object)_headers[i].HeaderInfo == (object)headerInfo)
                {
                    if (IsUnderstood(i))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void ReadBufferedHeader(XmlDictionaryReader reader, RecycledMessageState recycledMessageState, XmlDictionaryString[] localNames, bool understood)
        {
            if (MessageVersion.Addressing == AddressingVersion.None && reader.NamespaceURI == AddressingVersion.None.Namespace)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new InvalidOperationException(SR.Format(SR.AddressingHeadersCannotBeAddedToAddressingVersion, MessageVersion.Addressing)));
            }

            MessageHeader.GetHeaderAttributes(reader, MessageVersion, out string actor, out bool mustUnderstand, out bool relay, out bool isRefParam);

            HeaderKind kind = HeaderKind.Unknown;
            MessageHeaderInfo info = null;

            if (MessageVersion.Envelope.IsUltimateDestinationActor(actor))
            {
                Fx.Assert(MessageVersion.Addressing.DictionaryNamespace != null, "non-None Addressing requires a non-null DictionaryNamespace");
                kind = (HeaderKind)reader.IndexOfLocalName(localNames, MessageVersion.Addressing.DictionaryNamespace);
                switch (kind)
                {
                    case HeaderKind.To:
                        info = ToHeader.ReadHeader(reader, MessageVersion.Addressing, recycledMessageState.UriCache, actor, mustUnderstand, relay);
                        break;
                    case HeaderKind.Action:
                        info = ActionHeader.ReadHeader(reader, MessageVersion.Addressing, actor, mustUnderstand, relay);
                        break;
                    case HeaderKind.MessageId:
                        info = MessageIDHeader.ReadHeader(reader, MessageVersion.Addressing, actor, mustUnderstand, relay);
                        break;
                    case HeaderKind.RelatesTo:
                        info = RelatesToHeader.ReadHeader(reader, MessageVersion.Addressing, actor, mustUnderstand, relay);
                        break;
                    case HeaderKind.ReplyTo:
                        info = ReplyToHeader.ReadHeader(reader, MessageVersion.Addressing, actor, mustUnderstand, relay);
                        break;
                    case HeaderKind.From:
                        info = FromHeader.ReadHeader(reader, MessageVersion.Addressing, actor, mustUnderstand, relay);
                        break;
                    case HeaderKind.FaultTo:
                        info = FaultToHeader.ReadHeader(reader, MessageVersion.Addressing, actor, mustUnderstand, relay);
                        break;
                    default:
                        kind = HeaderKind.Unknown;
                        break;
                }
            }

            if (info == null)
            {
                info = recycledMessageState.HeaderInfoCache.TakeHeaderInfo(reader, actor, mustUnderstand, relay, isRefParam);
                reader.Skip();
            }

            HeaderProcessing processing = mustUnderstand ? HeaderProcessing.MustUnderstand : 0;
            if (kind != HeaderKind.Unknown || understood)
            {
                processing |= HeaderProcessing.Understood;
                TraceUnderstood(info);
            }
            AddHeader(new Header(kind, info, processing));
        }

        internal void Recycle(HeaderInfoCache headerInfoCache)
        {
            for (int i = 0; i < Count; i++)
            {
                if (_headers[i].HeaderKind == HeaderKind.Unknown)
                {
                    headerInfoCache.ReturnHeaderInfo(_headers[i].HeaderInfo);
                }
            }
            Clear();
            CollectionVersion = 0;
            if (_understoodHeaders != null)
            {
                _understoodHeaders.Modified = false;
            }
        }

        internal void RemoveUnderstood(MessageHeaderInfo headerInfo)
        {
            if (headerInfo == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(headerInfo));
            }

            for (int i = 0; i < Count; i++)
            {
                if ((object)_headers[i].HeaderInfo == (object)headerInfo)
                {
                    if ((_headers[i].HeaderProcessing & HeaderProcessing.Understood) == 0)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(
                            SR.Format(SR.HeaderAlreadyNotUnderstood, headerInfo.Name, headerInfo.Namespace), nameof(headerInfo)));
                    }

                    _headers[i].HeaderProcessing &= ~HeaderProcessing.Understood;
                }
            }
        }

        public void RemoveAll(string name, string ns)
        {
            if (name == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(name));
            }

            if (ns == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(ns));
            }

            for (int i = Count - 1; i >= 0; i--)
            {
                MessageHeaderInfo info = _headers[i].HeaderInfo;
                if (info.Name == name && info.Namespace == ns)
                {
                    RemoveAt(i);
                }
            }
        }

        public void RemoveAt(int headerIndex)
        {
            if (headerIndex < 0 || headerIndex >= Count)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new ArgumentOutOfRangeException(nameof(headerIndex), headerIndex,
                    SR.Format(SR.ValueMustBeInRange, 0, Count)));
            }
            if (_bufferedMessageData != null && _headers[headerIndex].HeaderType == HeaderType.BufferedMessageHeader)
            {
                CaptureBufferedHeaders(headerIndex);
            }

            Array.Copy(_headers, headerIndex + 1, _headers, headerIndex, Count - headerIndex - 1);
            _headers[--Count] = new Header();
            CollectionVersion++;
        }

        internal void ReplaceAt(int headerIndex, MessageHeader header)
        {
            if (headerIndex < 0 || headerIndex >= Count)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new ArgumentOutOfRangeException(nameof(headerIndex), headerIndex,
                    SR.Format(SR.ValueMustBeInRange, 0, Count)));
            }

            if (header == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(header));
            }

            ReplaceAt(headerIndex, header, GetHeaderKind(header));
        }

        private void ReplaceAt(int headerIndex, MessageHeader header, HeaderKind kind)
        {
            HeaderProcessing processing = header.MustUnderstand ? HeaderProcessing.MustUnderstand : 0;
            if (kind != HeaderKind.Unknown)
            {
                processing |= HeaderProcessing.Understood;
            }

            if (header is ReadableMessageHeader readableMessageHeader)
            {
                _headers[headerIndex] = new Header(kind, readableMessageHeader, processing);
            }
            else
            {
                _headers[headerIndex] = new Header(kind, header, processing);
            }

            CollectionVersion++;
        }

        public void SetAction(XmlDictionaryString action)
        {
            if (action == null)
            {
                SetHeaderProperty(HeaderKind.Action, null);
            }
            else
            {
                SetActionHeader(ActionHeader.Create(action, MessageVersion.Addressing));
            }
        }

        internal void SetActionHeader(ActionHeader actionHeader)
        {
            SetHeaderProperty(HeaderKind.Action, actionHeader);
        }

        internal void SetFaultToHeader(FaultToHeader faultToHeader)
        {
            SetHeaderProperty(HeaderKind.FaultTo, faultToHeader);
        }

        internal void SetFromHeader(FromHeader fromHeader)
        {
            SetHeaderProperty(HeaderKind.From, fromHeader);
        }

        internal void SetMessageIDHeader(MessageIDHeader messageIDHeader)
        {
            SetHeaderProperty(HeaderKind.MessageId, messageIDHeader);
        }

        internal void SetRelatesTo(Uri relationshipType, UniqueId messageId)
        {
            if (relationshipType == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(relationshipType));
            }

            RelatesToHeader relatesToHeader;
            if (!ReferenceEquals(messageId, null))
            {
                relatesToHeader = RelatesToHeader.Create(messageId, MessageVersion.Addressing, relationshipType);
            }
            else
            {
                relatesToHeader = null;
            }

            SetRelatesTo(RelatesToHeader.ReplyRelationshipType, relatesToHeader);
        }

        private void SetRelatesTo(Uri relationshipType, RelatesToHeader relatesToHeader)
        {
            int index = FindRelatesTo(relationshipType, out UniqueId previousUniqueId);
            if (index >= 0)
            {
                if (relatesToHeader == null)
                {
                    RemoveAt(index);
                }
                else
                {
                    ReplaceAt(index, relatesToHeader, HeaderKind.RelatesTo);
                }
            }
            else if (relatesToHeader != null)
            {
                Add(relatesToHeader, HeaderKind.RelatesTo);
            }
        }

        internal void SetReplyToHeader(ReplyToHeader replyToHeader)
        {
            SetHeaderProperty(HeaderKind.ReplyTo, replyToHeader);
        }

        internal void SetToHeader(ToHeader toHeader)
        {
            SetHeaderProperty(HeaderKind.To, toHeader);
        }

        private void SetHeaderProperty(HeaderKind kind, MessageHeader header)
        {
            int index = FindHeaderProperty(kind);
            if (index >= 0)
            {
                if (header == null)
                {
                    RemoveAt(index);
                }
                else
                {
                    ReplaceAt(index, header, kind);
                }
            }
            else if (header != null)
            {
                Add(header, kind);
            }
        }

        public void WriteHeader(int headerIndex, XmlWriter writer)
        {
            WriteHeader(headerIndex, XmlDictionaryWriter.CreateDictionaryWriter(writer));
        }

        public void WriteHeader(int headerIndex, XmlDictionaryWriter writer)
        {
            WriteStartHeader(headerIndex, writer);
            WriteHeaderContents(headerIndex, writer);
            writer.WriteEndElement();
        }

        public void WriteStartHeader(int headerIndex, XmlWriter writer)
        {
            WriteStartHeader(headerIndex, XmlDictionaryWriter.CreateDictionaryWriter(writer));
        }

        public void WriteStartHeader(int headerIndex, XmlDictionaryWriter writer)
        {
            if (writer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(writer));
            }

            if (headerIndex < 0 || headerIndex >= Count)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new ArgumentOutOfRangeException(nameof(headerIndex), headerIndex,
                    SR.Format(SR.ValueMustBeInRange, 0, Count)));
            }
            switch (_headers[headerIndex].HeaderType)
            {
                case HeaderType.ReadableHeader:
                case HeaderType.WriteableHeader:
                    _headers[headerIndex].MessageHeader.WriteStartHeader(writer, MessageVersion);
                    break;
                case HeaderType.BufferedMessageHeader:
                    WriteStartBufferedMessageHeader(_bufferedMessageData, headerIndex, writer);
                    break;
                default:
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.InvalidEnumValue, _headers[headerIndex].HeaderType)));
            }
        }

        public void WriteHeaderContents(int headerIndex, XmlWriter writer)
        {
            WriteHeaderContents(headerIndex, XmlDictionaryWriter.CreateDictionaryWriter(writer));
        }

        public void WriteHeaderContents(int headerIndex, XmlDictionaryWriter writer)
        {
            if (writer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(writer));
            }

            if (headerIndex < 0 || headerIndex >= Count)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new ArgumentOutOfRangeException(nameof(headerIndex), headerIndex,
                    SR.Format(SR.ValueMustBeInRange, 0, Count)));
            }
            switch (_headers[headerIndex].HeaderType)
            {
                case HeaderType.ReadableHeader:
                case HeaderType.WriteableHeader:
                    _headers[headerIndex].MessageHeader.WriteHeaderContents(writer, MessageVersion);
                    break;
                case HeaderType.BufferedMessageHeader:
                    WriteBufferedMessageHeaderContents(_bufferedMessageData, headerIndex, writer);
                    break;
                default:
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.InvalidEnumValue, _headers[headerIndex].HeaderType)));
            }
        }

        private static void TraceUnderstood(MessageHeaderInfo info)
        {
            //if (DiagnosticUtility.ShouldTraceVerbose)
            //{
            //    TraceUtility.TraceEvent(TraceEventType.Verbose, TraceCode.UnderstoodMessageHeader,
            //        SR.TraceCodeUnderstoodMessageHeader,
            //        new MessageHeaderInfoTraceRecord(info), null, null);
            //}
        }

        private void WriteBufferedMessageHeader(IBufferedMessageData bufferedMessageData, int bufferedMessageHeaderIndex, XmlWriter writer)
        {
            using (XmlReader reader = GetBufferedMessageHeaderReader(bufferedMessageData, bufferedMessageHeaderIndex))
            {
                writer.WriteNode(reader, false);
            }
        }

        private void WriteStartBufferedMessageHeader(IBufferedMessageData bufferedMessageData, int bufferedMessageHeaderIndex, XmlWriter writer)
        {
            using (XmlReader reader = GetBufferedMessageHeaderReader(bufferedMessageData, bufferedMessageHeaderIndex))
            {
                writer.WriteStartElement(reader.Prefix, reader.LocalName, reader.NamespaceURI);
                writer.WriteAttributes(reader, false);
            }
        }

        private void WriteBufferedMessageHeaderContents(IBufferedMessageData bufferedMessageData, int bufferedMessageHeaderIndex, XmlWriter writer)
        {
            using (XmlReader reader = GetBufferedMessageHeaderReader(bufferedMessageData, bufferedMessageHeaderIndex))
            {
                if (!reader.IsEmptyElement)
                {
                    reader.ReadStartElement();
                    while (reader.NodeType != XmlNodeType.EndElement)
                    {
                        writer.WriteNode(reader, false);
                    }
                    reader.ReadEndElement();
                }
            }
        }

        private enum HeaderType : byte
        {
            Invalid,
            ReadableHeader,
            BufferedMessageHeader,
            WriteableHeader
        }

        private enum HeaderKind : byte
        {
            Action,
            FaultTo,
            From,
            MessageId,
            ReplyTo,
            RelatesTo,
            To,
            Unknown,
        }

        [Flags]
        private enum HeaderProcessing : byte
        {
            MustUnderstand = 0x1,
            Understood = 0x2,
        }

        private struct Header
        {
            private readonly MessageHeaderInfo _info;

            public Header(HeaderKind kind, MessageHeaderInfo info, HeaderProcessing processing)
            {
                HeaderKind = kind;
                HeaderType = HeaderType.BufferedMessageHeader;
                _info = info;
                HeaderProcessing = processing;
            }

            public Header(HeaderKind kind, ReadableMessageHeader readableHeader, HeaderProcessing processing)
            {
                HeaderKind = kind;
                HeaderType = HeaderType.ReadableHeader;
                _info = readableHeader;
                HeaderProcessing = processing;
            }

            public Header(HeaderKind kind, MessageHeader header, HeaderProcessing processing)
            {
                HeaderKind = kind;
                HeaderType = HeaderType.WriteableHeader;
                _info = header;
                HeaderProcessing = processing;
            }

            public HeaderType HeaderType { get; }

            public HeaderKind HeaderKind { get; }

            public MessageHeaderInfo HeaderInfo
            {
                get { return _info; }
            }

            public MessageHeader MessageHeader
            {
                get
                {
                    Fx.Assert(HeaderType == HeaderType.WriteableHeader || HeaderType == HeaderType.ReadableHeader, "");
                    return (MessageHeader)_info;
                }
            }

            public HeaderProcessing HeaderProcessing { get; set; }

            public ReadableMessageHeader ReadableHeader
            {
                get
                {
                    Fx.Assert(HeaderType == HeaderType.ReadableHeader, "");
                    return (ReadableMessageHeader)_info;
                }
            }
        }
    }
}
