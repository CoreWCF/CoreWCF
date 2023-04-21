// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.Runtime;
using Microsoft.Extensions.ObjectPool;

namespace CoreWCF.Channels
{
    internal class JsonMessageEncoderFactory : MessageEncoderFactory
    {
        private static readonly ContentEncoding[] s_applicationJsonContentEncoding = GetContentEncodingMap(JsonGlobals.ApplicationJsonMediaType);
        private readonly JsonMessageEncoder _messageEncoder;

        public JsonMessageEncoderFactory(Encoding writeEncoding, int maxReadPoolSize, int maxWritePoolSize, XmlDictionaryReaderQuotas quotas, bool crossDomainScriptAccessEnabled)
        {
            _messageEncoder = new JsonMessageEncoder(writeEncoding, maxReadPoolSize, maxWritePoolSize, quotas, crossDomainScriptAccessEnabled);
        }

        public override MessageEncoder Encoder => _messageEncoder;

        public override MessageVersion MessageVersion => _messageEncoder.MessageVersion;

        internal static string GetContentType(WebMessageEncodingBindingElement encodingElement)
        {
            if (encodingElement == null)
            {
                return WebMessageEncoderFactory.GetContentType(JsonGlobals.ApplicationJsonMediaType, TextEncoderDefaults.Encoding);
            }
            else
            {
                return WebMessageEncoderFactory.GetContentType(JsonGlobals.ApplicationJsonMediaType, encodingElement.WriteEncoding);
            }
        }

        private static ContentEncoding[] GetContentEncodingMap(string mediaType)
        {
            Encoding[] readEncodings = ContentTypeHelpers.GetSupportedEncodings();
            ContentEncoding[] map = new ContentEncoding[readEncodings.Length];
            for (int i = 0; i < readEncodings.Length; i++)
            {
                ContentEncoding contentEncoding = new ContentEncoding();
                contentEncoding.contentType = WebMessageEncoderFactory.GetContentType(mediaType, readEncodings[i]);
                contentEncoding.encoding = readEncodings[i];
                map[i] = contentEncoding;
            }

            return map;
        }

        internal class JsonMessageEncoder : MessageEncoder
        {
            private const int MaxPooledXmlReadersPerMessage = 2;

            // Double-checked locking pattern requires volatile for read/write synchronization
            private volatile SynchronizedPool<JsonBufferedMessageData> _bufferedReaderPool;
            private volatile SynchronizedPool<JsonBufferedMessageWriter> _bufferedWriterPool;
            private readonly int _maxReadPoolSize;
            private readonly int _maxWritePoolSize;
            private readonly OnXmlDictionaryReaderClose _onStreamedReaderClose;
            private readonly XmlDictionaryReaderQuotas _readerQuotas;
            private readonly XmlDictionaryReaderQuotas _bufferedReadReaderQuotas;

            // Double-checked locking pattern requires volatile for read/write synchronization
            private volatile SynchronizedPool<RecycledMessageState> _recycledStatePool;
            private volatile SynchronizedPool<XmlDictionaryReader> _streamedReaderPool;
            private volatile SynchronizedPool<XmlDictionaryWriter> _streamedWriterPool;
            private readonly Encoding _writeEncoding;

            public JsonMessageEncoder(Encoding writeEncoding, int maxReadPoolSize, int maxWritePoolSize, XmlDictionaryReaderQuotas quotas, bool crossDomainScriptAccessEnabled)
            {
                if (writeEncoding == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(writeEncoding));
                }

                ThisLock = new object();

                TextEncoderDefaults.ValidateEncoding(writeEncoding);
                _writeEncoding = writeEncoding;

                _maxReadPoolSize = maxReadPoolSize;
                _maxWritePoolSize = maxWritePoolSize;

                _readerQuotas = new XmlDictionaryReaderQuotas();
                _onStreamedReaderClose = new OnXmlDictionaryReaderClose(ReturnStreamedReader);
                quotas.CopyTo(_readerQuotas);

                _bufferedReadReaderQuotas = EncoderHelpers.GetBufferedReadQuotas(_readerQuotas);

                ContentType = WebMessageEncoderFactory.GetContentType(JsonGlobals.ApplicationJsonMediaType, writeEncoding);
            }

            public override string ContentType { get; }

            public override string MediaType => JsonGlobals.ApplicationJsonMediaType;

            public override MessageVersion MessageVersion => MessageVersion.None;

            private SynchronizedPool<RecycledMessageState> RecycledStatePool
            {
                get
                {
                    if (_recycledStatePool == null)
                    {
                        lock (ThisLock)
                        {
                            if (_recycledStatePool == null)
                            {
                                _recycledStatePool = new SynchronizedPool<RecycledMessageState>(_maxReadPoolSize);
                            }
                        }
                    }

                    return _recycledStatePool;
                }
            }

            private object ThisLock { get; }

            public override bool IsContentTypeSupported(string contentType)
            {
                if (contentType == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(contentType));
                }

                return IsJsonContentType(contentType);
            }

            public override Message ReadMessage(ArraySegment<byte> buffer, BufferManager bufferManager, string contentType)
            {
                if (bufferManager == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(bufferManager)));
                }

                //if (WebTD.JsonMessageDecodingStartIsEnabled())
                //{
                //    WebTD.JsonMessageDecodingStart();
                //}

                Message message;

                JsonBufferedMessageData messageData = TakeBufferedReader();
                messageData.Encoding = ContentTypeHelpers.GetEncodingFromContentType(contentType, JsonMessageEncoderFactory.s_applicationJsonContentEncoding);
                messageData.Open(buffer, bufferManager);
                RecycledMessageState messageState = messageData.TakeMessageState();
                if (messageState == null)
                {
                    messageState = new RecycledMessageState();
                }
                message = new BufferedMessage(messageData, messageState);

                message.Properties.Encoder = this;

                //if (SMTD.MessageReadByEncoderIsEnabled() && buffer != null)
                //{
                //    SMTD.MessageReadByEncoder(
                //        EventTraceActivityHelper.TryExtractActivity(message, true),
                //        buffer.Count,
                //        this);
                //}

                //if (MessageLogger.LogMessagesAtTransportLevel)
                //{
                //    MessageLogger.LogMessage(ref message, MessageLoggingSource.TransportReceive);
                //}

                return message;
            }

            public override Task<Message> ReadMessageAsync(Stream stream, int maxSizeOfHeaders, string contentType)
            {
                if (stream == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(stream)));
                }

                //if (WebTD.JsonMessageDecodingStartIsEnabled())
                //{
                //    WebTD.JsonMessageDecodingStart();
                //}

                XmlReader reader = TakeStreamedReader(stream, ContentTypeHelpers.GetEncodingFromContentType(contentType, JsonMessageEncoderFactory.s_applicationJsonContentEncoding));
                Message message = Message.CreateMessage(reader, maxSizeOfHeaders, MessageVersion.None);
                message.Properties.Encoder = this;

                //if (SMTD.StreamedMessageReadByEncoderIsEnabled())
                //{
                //    SMTD.StreamedMessageReadByEncoder(EventTraceActivityHelper.TryExtractActivity(message, true));
                //}

                //if (MessageLogger.LogMessagesAtTransportLevel)
                //{
                //    MessageLogger.LogMessage(ref message, MessageLoggingSource.TransportReceive);
                //}

                return Task.FromResult(message);
            }

            public override ArraySegment<byte> WriteMessage(Message message, int maxMessageSize, BufferManager bufferManager, int messageOffset)
            {
                if (message == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(message)));
                }

                if (bufferManager == null)
                {
                    throw TraceUtility.ThrowHelperError(new ArgumentNullException(nameof(bufferManager)), message);
                }

                if (maxMessageSize < 0)
                {
                    throw TraceUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(maxMessageSize), maxMessageSize,
                        SR.Format(SRCommon.ValueMustBeNonNegative)), message);
                }

                if (messageOffset < 0 || messageOffset > maxMessageSize)
                {
                    throw TraceUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(messageOffset), messageOffset,
                        SR.Format(SR.JsonValueMustBeInRange, 0, maxMessageSize)), message);
                }

                //EventTraceActivity eventTraceActivity = null;
                //if (WebTD.JsonMessageEncodingStartIsEnabled())
                //{
                //    eventTraceActivity = EventTraceActivityHelper.TryExtractActivity(message);
                //    WebTD.JsonMessageEncodingStart(eventTraceActivity);
                //}

                ThrowIfMismatchedMessageVersion(message);
                message.Properties.Encoder = this;
                JsonBufferedMessageWriter messageWriter = TakeBufferedWriter();

                ArraySegment<byte> messageData = messageWriter.WriteMessage(message, bufferManager, messageOffset, maxMessageSize);
                ReturnMessageWriter(messageWriter);

                //if (SMTD.MessageWrittenByEncoderIsEnabled() && messageData != null)
                //{
                //    SMTD.MessageWrittenByEncoder(
                //        eventTraceActivity ?? EventTraceActivityHelper.TryExtractActivity(message),
                //        messageData.Count,
                //        this);
                //}

                //if (MessageLogger.LogMessagesAtTransportLevel)
                //{
                //    XmlDictionaryReader xmlDictionaryReader = JsonReaderWriterFactory.CreateJsonReader(
                //        messageData.Array, messageData.Offset, messageData.Count, null, XmlDictionaryReaderQuotas.Max, null);
                //    MessageLogger.LogMessage(ref message, xmlDictionaryReader, MessageLoggingSource.TransportSend);
                //}

                return messageData;
            }

            public override Task WriteMessageAsync(Message message, Stream stream)
            {
                if (message == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(message)));
                }

                if (stream == null)
                {
                    throw TraceUtility.ThrowHelperError(new ArgumentNullException(nameof(stream)), message);
                }

                ThrowIfMismatchedMessageVersion(message);

                //EventTraceActivity eventTraceActivity = null;
                //if (WebTD.JsonMessageEncodingStartIsEnabled())
                //{
                //    eventTraceActivity = EventTraceActivityHelper.TryExtractActivity(message);
                //    WebTD.JsonMessageEncodingStart(eventTraceActivity);
                //}

                message.Properties.Encoder = this;
                XmlDictionaryWriter xmlWriter = TakeStreamedWriter(stream);

                xmlWriter.WriteStartDocument();
                message.WriteMessage(xmlWriter);
                xmlWriter.WriteEndDocument();
                xmlWriter.Flush();
                ReturnStreamedWriter(xmlWriter);

                //if (SMTD.StreamedMessageWrittenByEncoderIsEnabled())
                //{
                //    SMTD.StreamedMessageWrittenByEncoder(
                //        eventTraceActivity ?? EventTraceActivityHelper.TryExtractActivity(message));
                //}

                //if (MessageLogger.LogMessagesAtTransportLevel)
                //{
                //    MessageLogger.LogMessage(ref message, MessageLoggingSource.TransportSend);
                //}

                return Task.CompletedTask;
            }

            protected override bool IsCharSetSupported(string charSet) => TextEncoderDefaults.TryGetEncoding(charSet, out _);

            private bool IsJsonContentType(string contentType) => IsContentTypeSupported(contentType, JsonGlobals.ApplicationJsonMediaType, JsonGlobals.ApplicationJsonMediaType) || IsContentTypeSupported(contentType, JsonGlobals.TextJsonMediaType, JsonGlobals.TextJsonMediaType);

            private void ReturnBufferedData(JsonBufferedMessageData messageData)
            {
                _bufferedReaderPool.Return(messageData);
            }

            private void ReturnMessageWriter(JsonBufferedMessageWriter messageWriter)
            {
                _bufferedWriterPool.Return(messageWriter);
            }

            private void ReturnStreamedReader(XmlDictionaryReader xmlReader)
            {
                _streamedReaderPool.Return(xmlReader);
            }

            private void ReturnStreamedWriter(XmlWriter xmlWriter)
            {
                xmlWriter.Close();
                _streamedWriterPool.Return((XmlDictionaryWriter)xmlWriter);
            }

            private JsonBufferedMessageData TakeBufferedReader()
            {
                if (_bufferedReaderPool == null)
                {
                    lock (ThisLock)
                    {
                        if (_bufferedReaderPool == null)
                        {
                            _bufferedReaderPool = new SynchronizedPool<JsonBufferedMessageData>(_maxReadPoolSize);
                        }
                    }
                }

                JsonBufferedMessageData messageData = _bufferedReaderPool.Take();
                if (messageData == null)
                {
                    messageData = new JsonBufferedMessageData(this, MaxPooledXmlReadersPerMessage);
                }

                return messageData;
            }

            private JsonBufferedMessageWriter TakeBufferedWriter()
            {
                if (_bufferedWriterPool == null)
                {
                    lock (ThisLock)
                    {
                        if (_bufferedWriterPool == null)
                        {
                            _bufferedWriterPool = new SynchronizedPool<JsonBufferedMessageWriter>(_maxWritePoolSize);
                        }
                    }
                }

                JsonBufferedMessageWriter messageWriter = _bufferedWriterPool.Take();
                if (messageWriter == null)
                {
                    messageWriter = new JsonBufferedMessageWriter(this);
                }

                return messageWriter;
            }

            private XmlDictionaryReader TakeStreamedReader(Stream stream, Encoding enc)
            {
                if (_streamedReaderPool == null)
                {
                    lock (ThisLock)
                    {
                        if (_streamedReaderPool == null)
                        {
                            _streamedReaderPool = new SynchronizedPool<XmlDictionaryReader>(_maxReadPoolSize);
                        }
                    }
                }

                XmlDictionaryReader xmlReader = _streamedReaderPool.Take();
                if (xmlReader == null)
                {
                    xmlReader = JsonReaderWriterFactory.CreateJsonReader(stream, enc, _readerQuotas, _onStreamedReaderClose);
                }
                else
                {
                    ((IXmlJsonReaderInitializer)xmlReader).SetInput(stream, enc, _readerQuotas, _onStreamedReaderClose);
                }

                return xmlReader;
            }

            private XmlDictionaryWriter TakeStreamedWriter(Stream stream)
            {
                if (_streamedWriterPool == null)
                {
                    lock (ThisLock)
                    {
                        if (_streamedWriterPool == null)
                        {
                            _streamedWriterPool = new SynchronizedPool<XmlDictionaryWriter>(_maxWritePoolSize);
                        }
                    }
                }

                XmlDictionaryWriter xmlWriter = _streamedWriterPool.Take();
                if (xmlWriter == null)
                {
                    xmlWriter = JsonReaderWriterFactory.CreateJsonWriter(stream, _writeEncoding, false);
                }
                else
                {
                    ((IXmlJsonWriterInitializer)xmlWriter).SetOutput(stream, _writeEncoding, false);
                }

                return xmlWriter;
            }

            internal class JsonBufferedMessageData : BufferedMessageData
            {
                private Encoding _encoding;
                private readonly JsonMessageEncoder _messageEncoder;
                private readonly OnXmlDictionaryReaderClose _onClose;
                private readonly Pool<XmlDictionaryReader> _readerPool;

                public JsonBufferedMessageData(JsonMessageEncoder messageEncoder, int maxReaderPoolSize)
                    : base(messageEncoder.RecycledStatePool)
                {
                    _messageEncoder = messageEncoder;
                    _onClose = new OnXmlDictionaryReaderClose(OnXmlReaderClosed);
                    _readerPool = new Pool<XmlDictionaryReader>(maxReaderPoolSize);
                }

                public override MessageEncoder MessageEncoder => _messageEncoder;

                public override XmlDictionaryReaderQuotas Quotas => _messageEncoder._bufferedReadReaderQuotas;

                internal Encoding Encoding
                {
                    set
                    {
                        _encoding = value;
                    }
                }

                protected override void OnClosed()
                {
                    _messageEncoder.ReturnBufferedData(this);
                }

                protected override void ReturnXmlReader(XmlDictionaryReader xmlReader)
                {
                    if (xmlReader != null)
                    {
                        _readerPool.Return(xmlReader);
                    }
                }

                protected override XmlDictionaryReader TakeXmlReader()
                {
                    ArraySegment<byte> buffer = Buffer;

                    XmlDictionaryReader xmlReader = _readerPool.Take();
                    if (xmlReader == null)
                    {
                        xmlReader = JsonReaderWriterFactory.CreateJsonReader(buffer.Array, buffer.Offset, buffer.Count, _encoding, Quotas, _onClose);
                    }
                    else
                    {
                        ((IXmlJsonReaderInitializer)xmlReader).SetInput(buffer.Array, buffer.Offset, buffer.Count, _encoding, Quotas, _onClose);
                    }

                    return xmlReader;
                }
            }

            internal class JsonBufferedMessageWriter : BufferedMessageWriter
            {
                private readonly JsonMessageEncoder _messageEncoder;
                private XmlDictionaryWriter _returnedWriter;

                public JsonBufferedMessageWriter(JsonMessageEncoder messageEncoder)
                {
                    _messageEncoder = messageEncoder;
                }

                protected override void OnWriteEndMessage(XmlDictionaryWriter writer)
                {
                    writer.WriteEndDocument();
                }

                protected override void OnWriteStartMessage(XmlDictionaryWriter writer)
                {
                    writer.WriteStartDocument();
                }

                protected override void ReturnXmlWriter(XmlDictionaryWriter writer)
                {
                    writer.Close();

                    if (_returnedWriter == null)
                    {
                        _returnedWriter = writer;
                    }
                }

                protected override XmlDictionaryWriter TakeXmlWriter(Stream stream)
                {
                    XmlDictionaryWriter writer;
                    if (_returnedWriter == null)
                    {
                        writer = JsonReaderWriterFactory.CreateJsonWriter(stream, _messageEncoder._writeEncoding, false);
                    }
                    else
                    {
                        writer = _returnedWriter;
                        ((IXmlJsonWriterInitializer)writer).SetOutput(stream, _messageEncoder._writeEncoding, false);
                        _returnedWriter = null;
                    }

                    return writer;
                }
            }
        }
    }
}
