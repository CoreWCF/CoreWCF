// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.Runtime;
using CoreWCF.Xml;

namespace CoreWCF.Channels
{
    internal class BinaryMessageEncoderFactory : MessageEncoderFactory
    {
        private const int maxPooledXmlReaderPerMessage = 2;
        private readonly BinaryMessageEncoder _messageEncoder;
        private readonly MessageVersion _messageVersion;

        // Double-checked locking pattern requires volatile for read/write synchronization
        //volatile SynchronizedPool<XmlDictionaryWriter> streamedWriterPool;
        //volatile SynchronizedPool<XmlDictionaryReader> streamedReaderPool;
        private volatile SynchronizedPool<BinaryBufferedMessageData> _bufferedDataPool;
        private volatile SynchronizedPool<BinaryBufferedMessageWriter> _bufferedWriterPool;
        private volatile SynchronizedPool<RecycledMessageState> _recycledStatePool;
        private readonly OnXmlDictionaryReaderClose _onStreamedReaderClose;
        private readonly XmlDictionaryReaderQuotas _bufferedReadReaderQuotas;
        private readonly BinaryVersion _binaryVersion;

        public BinaryMessageEncoderFactory(MessageVersion messageVersion, int maxReadPoolSize, int maxWritePoolSize, int maxSessionSize,
            XmlDictionaryReaderQuotas readerQuotas, long maxReceivedMessageSize, BinaryVersion version, CompressionFormat compressionFormat)
        {
            _messageVersion = messageVersion;
            MaxReadPoolSize = maxReadPoolSize;
            MaxWritePoolSize = maxWritePoolSize;
            MaxSessionSize = maxSessionSize;
            ThisLock = new object();
            _onStreamedReaderClose = new OnXmlDictionaryReaderClose(ReturnStreamedReader);
            ReaderQuotas = new XmlDictionaryReaderQuotas();
            if (readerQuotas != null)
            {
                readerQuotas.CopyTo(ReaderQuotas);
            }

            _bufferedReadReaderQuotas = EncoderHelpers.GetBufferedReadQuotas(ReaderQuotas);
            MaxReceivedMessageSize = maxReceivedMessageSize;

            _binaryVersion = version;
            CompressionFormat = compressionFormat;
            _messageEncoder = new BinaryMessageEncoder(this, false, 0);
        }

        public static IXmlDictionary XmlDictionary
        {
            get { return XD.Dictionary; }
        }

        public override MessageEncoder Encoder
        {
            get
            {
                return _messageEncoder;
            }
        }

        public override MessageVersion MessageVersion
        {
            get { return _messageVersion; }
        }

        public int MaxWritePoolSize { get; }

        public XmlDictionaryReaderQuotas ReaderQuotas { get; }

        public int MaxReadPoolSize { get; }

        public int MaxSessionSize { get; }

        public CompressionFormat CompressionFormat { get; }

        private long MaxReceivedMessageSize
        {
            get;
            set;
        }

        private object ThisLock { get; }

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
                            //running = true;
                            _recycledStatePool = new SynchronizedPool<RecycledMessageState>(MaxReadPoolSize);
                        }
                    }
                }
                return _recycledStatePool;
            }
        }

        public override MessageEncoder CreateSessionEncoder()
        {
            return new BinaryMessageEncoder(this, true, MaxSessionSize);
        }

        private XmlDictionaryWriter TakeStreamedWriter(Stream stream)
        {
            return XmlDictionaryWriter.CreateBinaryWriter(stream, _binaryVersion.Dictionary, null, false);
            // TODO: Revert once IXmlBinaryWriterInitializer is available
            //if (streamedWriterPool == null)
            //{
            //    lock (ThisLock)
            //    {
            //        if (streamedWriterPool == null)
            //        {
            //            //running = true;
            //            streamedWriterPool = new SynchronizedPool<XmlDictionaryWriter>(maxWritePoolSize);
            //        }
            //    }
            //}
            //XmlDictionaryWriter xmlWriter = streamedWriterPool.Take();
            //if (xmlWriter == null)
            //{
            //    xmlWriter = XmlDictionaryWriter.CreateBinaryWriter(stream, binaryVersion.Dictionary, null, false);
            //}
            //else
            //{
            //    ((IXmlBinaryWriterInitializer)xmlWriter).SetOutput(stream, binaryVersion.Dictionary, null, false);
            //}
            //return xmlWriter;
        }

        private void ReturnStreamedWriter(XmlDictionaryWriter xmlWriter)
        {
            xmlWriter.Dispose();
            //streamedWriterPool.Return(xmlWriter);
        }

        private BinaryBufferedMessageWriter TakeBufferedWriter()
        {
            if (_bufferedWriterPool == null)
            {
                lock (ThisLock)
                {
                    if (_bufferedWriterPool == null)
                    {
                        //running = true;
                        _bufferedWriterPool = new SynchronizedPool<BinaryBufferedMessageWriter>(MaxWritePoolSize);
                    }
                }
            }

            BinaryBufferedMessageWriter messageWriter = _bufferedWriterPool.Take();
            if (messageWriter == null)
            {
                messageWriter = new BinaryBufferedMessageWriter(_binaryVersion.Dictionary);
            }
            return messageWriter;
        }

        private void ReturnMessageWriter(BinaryBufferedMessageWriter messageWriter)
        {
            _bufferedWriterPool.Return(messageWriter);
        }

        private XmlDictionaryReader TakeStreamedReader(Stream stream)
        {
            return XmlDictionaryReader.CreateBinaryReader(stream,
                _binaryVersion.Dictionary,
                ReaderQuotas,
                null);
            // TODO: Revert once IXmlBinaryReaderInitializer is available
            //if (streamedReaderPool == null)
            //{
            //    lock (ThisLock)
            //    {
            //        if (streamedReaderPool == null)
            //        {
            //            //running = true;
            //            streamedReaderPool = new SynchronizedPool<XmlDictionaryReader>(maxReadPoolSize);
            //        }
            //    }
            //}

            //XmlDictionaryReader xmlReader = streamedReaderPool.Take();
            //if (xmlReader == null)
            //{
            //    xmlReader = XmlDictionaryReader.CreateBinaryReader(stream,
            //        binaryVersion.Dictionary,
            //        readerQuotas,
            //        null,
            //        onStreamedReaderClose);
            //    if (TD.ReadPoolMissIsEnabled())
            //    {
            //        TD.ReadPoolMiss(xmlReader.GetType().Name);
            //    }
            //}
            //else
            //{
            //    ((IXmlBinaryReaderInitializer)xmlReader).SetInput(stream,
            //        binaryVersion.Dictionary,
            //        readerQuotas,
            //        null,
            //        onStreamedReaderClose);
            //}

            //return xmlReader;
        }

        private void ReturnStreamedReader(XmlDictionaryReader xmlReader)
        {
            //streamedReaderPool.Return(xmlReader);
        }

        private BinaryBufferedMessageData TakeBufferedData(BinaryMessageEncoder messageEncoder)
        {
            if (_bufferedDataPool == null)
            {
                lock (ThisLock)
                {
                    if (_bufferedDataPool == null)
                    {
                        //running = true;
                        _bufferedDataPool = new SynchronizedPool<BinaryBufferedMessageData>(MaxReadPoolSize);
                    }
                }
            }
            BinaryBufferedMessageData messageData = _bufferedDataPool.Take();
            if (messageData == null)
            {
                messageData = new BinaryBufferedMessageData(this, maxPooledXmlReaderPerMessage);
            }
            messageData.SetMessageEncoder(messageEncoder);
            return messageData;
        }

        private void ReturnBufferedData(BinaryBufferedMessageData messageData)
        {
            messageData.SetMessageEncoder(null);
            _bufferedDataPool.Return(messageData);
        }

        private class BinaryBufferedMessageData : BufferedMessageData
        {
            private readonly BinaryMessageEncoderFactory _factory;
            private BinaryMessageEncoder _messageEncoder;
            private readonly Pool<XmlDictionaryReader> _readerPool;
            private readonly OnXmlDictionaryReaderClose _onClose;

            public BinaryBufferedMessageData(BinaryMessageEncoderFactory factory, int maxPoolSize)
                : base(factory.RecycledStatePool)
            {
                _factory = factory;
                _readerPool = new Pool<XmlDictionaryReader>(maxPoolSize);
                _onClose = new OnXmlDictionaryReaderClose(OnXmlReaderClosed);
            }

            public override MessageEncoder MessageEncoder
            {
                get { return _messageEncoder; }
            }

            public override XmlDictionaryReaderQuotas Quotas
            {
                get { return _factory.ReaderQuotas; }
            }

            public void SetMessageEncoder(BinaryMessageEncoder messageEncoder)
            {
                _messageEncoder = messageEncoder;
            }

            protected override XmlDictionaryReader TakeXmlReader()
            {
                ArraySegment<byte> buffer = Buffer;

                return XmlDictionaryReader.CreateBinaryReader(buffer.Array, buffer.Offset, buffer.Count,
                                        _factory._binaryVersion.Dictionary,
                                        _factory._bufferedReadReaderQuotas,
                                        _messageEncoder.ReaderSession);
                // TODO: Revert once IXmlBinaryReaderInitializer is available
                //ArraySegment<byte> buffer = this.Buffer;
                //XmlDictionaryReader xmlReader = readerPool.Take();

                //if (xmlReader != null)
                //{
                //    ((IXmlBinaryReaderInitializer)xmlReader).SetInput(buffer.Array, buffer.Offset, buffer.Count,
                //        factory.binaryVersion.Dictionary,
                //        factory.bufferedReadReaderQuotas,
                //        messageEncoder.ReaderSession,
                //        onClose);
                //}
                //else
                //{
                //    xmlReader = XmlDictionaryReader.CreateBinaryReader(buffer.Array, buffer.Offset, buffer.Count,
                //        factory.binaryVersion.Dictionary,
                //        factory.bufferedReadReaderQuotas,
                //        messageEncoder.ReaderSession,
                //        onClose);
                //    if (TD.ReadPoolMissIsEnabled())
                //    {
                //        TD.ReadPoolMiss(xmlReader.GetType().Name);
                //    }
                //}

                //return xmlReader;
            }

            protected override void ReturnXmlReader(XmlDictionaryReader reader)
            {
                _readerPool.Return(reader);
            }

            protected override void OnClosed()
            {
                _factory.ReturnBufferedData(this);
            }
        }

        private class BinaryBufferedMessageWriter : BufferedMessageWriter
        {
            private XmlDictionaryWriter _writer;
            private readonly IXmlDictionary _dictionary;
            private readonly XmlBinaryWriterSession _session;

            public BinaryBufferedMessageWriter(IXmlDictionary dictionary)
            {
                _dictionary = dictionary;
            }

            public BinaryBufferedMessageWriter(IXmlDictionary dictionary, XmlBinaryWriterSession session)
            {
                _dictionary = dictionary;
                _session = session;
            }

            protected override XmlDictionaryWriter TakeXmlWriter(Stream stream)
            {
                return XmlDictionaryWriter.CreateBinaryWriter(stream, _dictionary, _session, false);
                // TODO: Revert once IXmlBinaryReaderInitializer is available
                //XmlDictionaryWriter returnedWriter = writer;
                //if (returnedWriter == null)
                //{
                //    returnedWriter = XmlDictionaryWriter.CreateBinaryWriter(stream, dictionary, session, false);
                //}
                //else
                //{
                //    writer = null;
                //    ((IXmlBinaryWriterInitializer)returnedWriter).SetOutput(stream, dictionary, session, false);
                //}
                //return returnedWriter;
            }

            protected override void ReturnXmlWriter(XmlDictionaryWriter writer)
            {
                writer.Dispose();

                if (_writer == null)
                {
                    _writer = writer;
                }
            }
        }

        private class BinaryMessageEncoder : MessageEncoder, ICompressedMessageEncoder
        {
            private const string SupportedCompressionTypesMessageProperty = "BinaryMessageEncoder.SupportedCompressionTypes";
            private readonly BinaryMessageEncoderFactory _factory;
            private readonly bool _isSession;
            private XmlBinaryWriterSessionWithQuota _writerSession;
            private BinaryBufferedMessageWriter _sessionMessageWriter;

            //XmlBinaryReaderSession readerSessionForLogging;
            //bool readerSessionForLoggingIsInvalid = false;
            //int writeIdCounter;
            private int _idCounter;
            private readonly int _maxSessionSize;
            private int _remainingReaderSessionSize;
            private bool _isReaderSessionInvalid;
            private MessagePatterns _messagePatterns;
            private readonly string _contentType;
            private readonly string _normalContentType;
            private readonly string _gzipCompressedContentType;
            private readonly string _deflateCompressedContentType;
            private CompressionFormat _sessionCompressionFormat;
            private readonly long _maxReceivedMessageSize;

            public BinaryMessageEncoder(BinaryMessageEncoderFactory factory, bool isSession, int maxSessionSize)
            {
                _factory = factory;
                _isSession = isSession;
                _maxSessionSize = maxSessionSize;
                _remainingReaderSessionSize = maxSessionSize;
                _normalContentType = isSession ? factory._binaryVersion.SessionContentType : factory._binaryVersion.ContentType;
                _gzipCompressedContentType = isSession ? BinaryVersion.GZipVersion1.SessionContentType : BinaryVersion.GZipVersion1.ContentType;
                _deflateCompressedContentType = isSession ? BinaryVersion.DeflateVersion1.SessionContentType : BinaryVersion.DeflateVersion1.ContentType;
                _sessionCompressionFormat = _factory.CompressionFormat;
                _maxReceivedMessageSize = _factory.MaxReceivedMessageSize;

                switch (_factory.CompressionFormat)
                {
                    case CompressionFormat.Deflate:
                        _contentType = _deflateCompressedContentType;
                        break;
                    case CompressionFormat.GZip:
                        _contentType = _gzipCompressedContentType;
                        break;
                    default:
                        _contentType = _normalContentType;
                        break;
                }
            }

            public override string ContentType
            {
                get
                {
                    return _contentType;
                }
            }

            public override MessageVersion MessageVersion
            {
                get { return _factory._messageVersion; }
            }

            public override string MediaType
            {
                get { return _contentType; }
            }

            public XmlBinaryReaderSession ReaderSession { get; private set; }

            public bool CompressionEnabled
            {
                get { return _factory.CompressionFormat != CompressionFormat.None; }
            }

            private ArraySegment<byte> AddSessionInformationToMessage(ArraySegment<byte> messageData, BufferManager bufferManager, int maxMessageSize)
            {
                int dictionarySize = 0;
                byte[] buffer = messageData.Array;

                if (_writerSession.HasNewStrings)
                {
                    IList<XmlDictionaryString> newStrings = _writerSession.GetNewStrings();
                    for (int i = 0; i < newStrings.Count; i++)
                    {
                        int utf8ValueSize = Encoding.UTF8.GetByteCount(newStrings[i].Value);
                        dictionarySize += IntEncoder.GetEncodedSize(utf8ValueSize) + utf8ValueSize;
                    }

                    int messageSize = messageData.Offset + messageData.Count;
                    int remainingMessageSize = maxMessageSize - messageSize;
                    if (remainingMessageSize - dictionarySize < 0)
                    {
                        string excMsg = SR.Format(SRCommon.MaxSentMessageSizeExceeded, maxMessageSize);
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new QuotaExceededException(excMsg));
                    }

                    int requiredBufferSize = messageData.Offset + messageData.Count + dictionarySize;
                    if (buffer.Length < requiredBufferSize)
                    {
                        byte[] newBuffer = bufferManager.TakeBuffer(requiredBufferSize);
                        Buffer.BlockCopy(buffer, messageData.Offset, newBuffer, messageData.Offset, messageData.Count);
                        bufferManager.ReturnBuffer(buffer);
                        buffer = newBuffer;
                    }

                    Buffer.BlockCopy(buffer, messageData.Offset, buffer, messageData.Offset + dictionarySize, messageData.Count);

                    int offset = messageData.Offset;
                    for (int i = 0; i < newStrings.Count; i++)
                    {
                        string newString = newStrings[i].Value;
                        int utf8ValueSize = Encoding.UTF8.GetByteCount(newString);
                        offset += IntEncoder.Encode(utf8ValueSize, buffer, offset);
                        offset += Encoding.UTF8.GetBytes(newString, 0, newString.Length, buffer, offset);
                    }

                    _writerSession.ClearNewStrings();
                }

                int headerSize = IntEncoder.GetEncodedSize(dictionarySize);
                int newOffset = messageData.Offset - headerSize;
                int newSize = headerSize + messageData.Count + dictionarySize;
                IntEncoder.Encode(dictionarySize, buffer, newOffset);
                return new ArraySegment<byte>(buffer, newOffset, newSize);
            }

            private ArraySegment<byte> ExtractSessionInformationFromMessage(ArraySegment<byte> messageData)
            {
                if (_isReaderSessionInvalid)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidDataException(SR.BinaryEncoderSessionInvalid));
                }

                byte[] buffer = messageData.Array;
                int dictionarySize;
                int headerSize;
                int newOffset;
                int newSize;
                bool throwing = true;
                try
                {
                    IntDecoder decoder = new IntDecoder();
                    headerSize = decoder.Decode(buffer, messageData.Offset, messageData.Count);
                    dictionarySize = decoder.Value;
                    if (dictionarySize > messageData.Count)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidDataException(SR.BinaryEncoderSessionMalformed));
                    }
                    newOffset = messageData.Offset + headerSize + dictionarySize;
                    newSize = messageData.Count - headerSize - dictionarySize;
                    if (newSize < 0)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidDataException(SR.BinaryEncoderSessionMalformed));
                    }
                    if (dictionarySize > 0)
                    {
                        if (dictionarySize > _remainingReaderSessionSize)
                        {
                            string message = SR.Format(SR.BinaryEncoderSessionTooLarge, _maxSessionSize);
                            Exception inner = new QuotaExceededException(message);
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CommunicationException(message, inner));
                        }
                        else
                        {
                            _remainingReaderSessionSize -= dictionarySize;
                        }

                        int size = dictionarySize;
                        int offset = messageData.Offset + headerSize;

                        while (size > 0)
                        {
                            decoder.Reset();
                            int bytesDecoded = decoder.Decode(buffer, offset, size);
                            int utf8ValueSize = decoder.Value;
                            offset += bytesDecoded;
                            size -= bytesDecoded;
                            if (utf8ValueSize > size)
                            {
                                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidDataException(SR.BinaryEncoderSessionMalformed));
                            }
                            string value = Encoding.UTF8.GetString(buffer, offset, utf8ValueSize);
                            offset += utf8ValueSize;
                            size -= utf8ValueSize;
                            ReaderSession.Add(_idCounter, value);
                            _idCounter++;
                        }
                    }
                    throwing = false;
                }
                finally
                {
                    if (throwing)
                    {
                        _isReaderSessionInvalid = true;
                    }
                }

                return new ArraySegment<byte>(buffer, newOffset, newSize);
            }

            public override Message ReadMessage(ArraySegment<byte> buffer, BufferManager bufferManager, string contentType)
            {
                if (bufferManager == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(bufferManager));
                }

                CompressionFormat compressionFormat = CheckContentType(contentType);

                if (compressionFormat != CompressionFormat.None)
                {
                    MessageEncoderCompressionHandler.DecompressBuffer(ref buffer, bufferManager, compressionFormat, _maxReceivedMessageSize);
                }

                if (_isSession)
                {
                    if (ReaderSession == null)
                    {
                        ReaderSession = new XmlBinaryReaderSession();
                        _messagePatterns = new MessagePatterns(_factory._binaryVersion.Dictionary, ReaderSession, MessageVersion);
                    }
                    try
                    {
                        buffer = ExtractSessionInformationFromMessage(buffer);
                    }
                    catch (InvalidDataException)
                    {
                        //MessageLogger.LogMessage(buffer, MessageLoggingSource.Malformed);
                        throw;
                    }
                }
                BinaryBufferedMessageData messageData = _factory.TakeBufferedData(this);
                Message message;
                if (_messagePatterns != null)
                {
                    message = _messagePatterns.TryCreateMessage(buffer.Array, buffer.Offset, buffer.Count, bufferManager, messageData);
                }
                else
                {
                    message = null;
                }
                if (message == null)
                {
                    messageData.Open(buffer, bufferManager);
                    RecycledMessageState messageState = messageData.TakeMessageState();
                    if (messageState == null)
                    {
                        messageState = new RecycledMessageState();
                    }
                    message = new BufferedMessage(messageData, messageState);
                }
                message.Properties.Encoder = this;

                return message;
            }

            public override Task<Message> ReadMessageAsync(Stream stream, int maxSizeOfHeaders, string contentType)
            {
                if (stream == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(stream));
                }

                CompressionFormat compressionFormat = CheckContentType(contentType);

                if (compressionFormat != CompressionFormat.None)
                {
                    stream = new MaxMessageSizeStream(
                        MessageEncoderCompressionHandler.GetDecompressStream(stream, compressionFormat), _maxReceivedMessageSize);
                }

                XmlDictionaryReader reader = _factory.TakeStreamedReader(stream);
                Message message = Message.CreateMessage(reader, maxSizeOfHeaders, _factory._messageVersion);
                message.Properties.Encoder = this;

                return Task.FromResult(message);
            }

            public override ArraySegment<byte> WriteMessage(Message message, int maxMessageSize, BufferManager bufferManager, int messageOffset)
            {
                if (message == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));
                }

                if (bufferManager == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(bufferManager));
                }

                if (maxMessageSize < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(maxMessageSize), maxMessageSize,
                        SRCommon.ValueMustBeNonNegative));
                }

                message.Properties.Encoder = this;

                if (_isSession)
                {
                    if (_writerSession == null)
                    {
                        _writerSession = new XmlBinaryWriterSessionWithQuota(_maxSessionSize);
                        _sessionMessageWriter = new BinaryBufferedMessageWriter(_factory._binaryVersion.Dictionary, _writerSession);
                    }
                    messageOffset += IntEncoder.MaxEncodedSize;
                }

                if (messageOffset < 0)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(messageOffset), messageOffset,
                        SRCommon.ValueMustBeNonNegative));
                }

                if (messageOffset > maxMessageSize)
                {
                    string excMsg = SR.Format(SRCommon.MaxSentMessageSizeExceeded, maxMessageSize);

                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new QuotaExceededException(excMsg));
                }

                ThrowIfMismatchedMessageVersion(message);
                BinaryBufferedMessageWriter messageWriter;
                if (_isSession)
                {
                    messageWriter = _sessionMessageWriter;
                }
                else
                {
                    messageWriter = _factory.TakeBufferedWriter();
                }
                ArraySegment<byte> messageData = messageWriter.WriteMessage(message, bufferManager, messageOffset, maxMessageSize);

                //this.readerSessionForLoggingIsInvalid = true;

                if (_isSession)
                {
                    messageData = AddSessionInformationToMessage(messageData, bufferManager, maxMessageSize);
                }
                else
                {
                    _factory.ReturnMessageWriter(messageWriter);
                }

                CompressionFormat compressionFormat = CheckCompressedWrite(message);
                if (compressionFormat != CompressionFormat.None)
                {
                    MessageEncoderCompressionHandler.CompressBuffer(ref messageData, bufferManager, compressionFormat);
                }

                return messageData;
            }

            public override async Task WriteMessageAsync(Message message, Stream stream)
            {
                if (message == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(message)));
                }
                if (stream == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(stream)));
                }

                CompressionFormat compressionFormat = CheckCompressedWrite(message);
                if (compressionFormat != CompressionFormat.None)
                {
                    stream = MessageEncoderCompressionHandler.GetCompressStream(stream, compressionFormat);
                }

                ThrowIfMismatchedMessageVersion(message);
                message.Properties.Encoder = this;
                XmlDictionaryWriter xmlWriter = _factory.TakeStreamedWriter(stream);
                await message.WriteMessageAsync(xmlWriter);
                xmlWriter.Flush();

                _factory.ReturnStreamedWriter(xmlWriter);
                if (compressionFormat != CompressionFormat.None)
                {
                    stream.Dispose();
                }
            }

            public override bool IsContentTypeSupported(string contentType)
            {
                bool supported = true;
                if (!base.IsContentTypeSupported(contentType))
                {
                    if (CompressionEnabled)
                    {
                        supported = (_factory.CompressionFormat == CompressionFormat.GZip &&
                            IsContentTypeSupported(contentType, _gzipCompressedContentType, _gzipCompressedContentType)) ||
                            (_factory.CompressionFormat == CompressionFormat.Deflate &&
                            IsContentTypeSupported(contentType, _deflateCompressedContentType, _deflateCompressedContentType)) ||
                            IsContentTypeSupported(contentType, _normalContentType, _normalContentType);
                    }
                    else
                    {
                        supported = false;
                    }
                }
                return supported;
            }

            public void SetSessionContentType(string contentType)
            {
                if (IsContentTypeSupported(contentType, _gzipCompressedContentType, _gzipCompressedContentType))
                {
                    _sessionCompressionFormat = CompressionFormat.GZip;
                }
                else if (IsContentTypeSupported(contentType, _deflateCompressedContentType, _deflateCompressedContentType))
                {
                    _sessionCompressionFormat = CompressionFormat.Deflate;
                }
                else
                {
                    _sessionCompressionFormat = CompressionFormat.None;
                }
            }

            public void AddCompressedMessageProperties(Message message, string supportedCompressionTypes)
            {
                message.Properties.Add(SupportedCompressionTypesMessageProperty, supportedCompressionTypes);
            }

            private static bool ContentTypeEqualsOrStartsWith(string contentType, string supportedContentType)
            {
                return contentType == supportedContentType || contentType.StartsWith(supportedContentType, StringComparison.OrdinalIgnoreCase);
            }

            private CompressionFormat CheckContentType(string contentType)
            {
                CompressionFormat compressionFormat = CompressionFormat.None;
                if (contentType == null)
                {
                    compressionFormat = _sessionCompressionFormat;
                }
                else
                {
                    if (!CompressionEnabled)
                    {
                        if (!ContentTypeEqualsOrStartsWith(contentType, ContentType))
                        {
                            throw Fx.Exception.AsError(new ProtocolException(SR.Format(SR.EncoderUnrecognizedContentType, contentType, ContentType)));
                        }
                    }
                    else
                    {
                        if (_factory.CompressionFormat == CompressionFormat.GZip && ContentTypeEqualsOrStartsWith(contentType, _gzipCompressedContentType))
                        {
                            compressionFormat = CompressionFormat.GZip;
                        }
                        else if (_factory.CompressionFormat == CompressionFormat.Deflate && ContentTypeEqualsOrStartsWith(contentType, _deflateCompressedContentType))
                        {
                            compressionFormat = CompressionFormat.Deflate;
                        }
                        else if (ContentTypeEqualsOrStartsWith(contentType, _normalContentType))
                        {
                            compressionFormat = CompressionFormat.None;
                        }
                        else
                        {
                            throw Fx.Exception.AsError(new ProtocolException(SR.Format(SR.EncoderUnrecognizedContentType, contentType, ContentType)));
                        }
                    }
                }

                return compressionFormat;
            }

            private CompressionFormat CheckCompressedWrite(Message message)
            {
                CompressionFormat compressionFormat = _sessionCompressionFormat;
                if (compressionFormat != CompressionFormat.None && !_isSession)
                {
                    if (message.Properties.TryGetValue<string>(SupportedCompressionTypesMessageProperty, out string acceptEncoding) &&
                        acceptEncoding != null)
                    {
                        acceptEncoding = acceptEncoding.ToLowerInvariant();
                        if ((compressionFormat == CompressionFormat.GZip &&
                            !acceptEncoding.Contains(MessageEncoderCompressionHandler.GZipContentEncoding)) ||
                            (compressionFormat == CompressionFormat.Deflate &&
                            !acceptEncoding.Contains(MessageEncoderCompressionHandler.DeflateContentEncoding)))
                        {
                            compressionFormat = CompressionFormat.None;
                        }
                    }
                }
                return compressionFormat;
            }
        }

        private class XmlBinaryWriterSessionWithQuota : XmlBinaryWriterSession
        {
            private int _bytesRemaining;
            private List<XmlDictionaryString> _newStrings;

            public XmlBinaryWriterSessionWithQuota(int maxSessionSize)
            {
                _bytesRemaining = maxSessionSize;
            }

            public bool HasNewStrings
            {
                get { return _newStrings != null; }
            }

            public override bool TryAdd(XmlDictionaryString s, out int key)
            {
                if (_bytesRemaining == 0)
                {
                    key = -1;
                    return false;
                }

                int bytesRequired = Encoding.UTF8.GetByteCount(s.Value);
                bytesRequired += IntEncoder.GetEncodedSize(bytesRequired);

                if (bytesRequired > _bytesRemaining)
                {
                    key = -1;
                    _bytesRemaining = 0;
                    return false;
                }

                if (base.TryAdd(s, out key))
                {
                    if (_newStrings == null)
                    {
                        _newStrings = new List<XmlDictionaryString>();
                    }
                    _newStrings.Add(s);
                    _bytesRemaining -= bytesRequired;
                    return true;
                }
                else
                {
                    return false;
                }
            }

            public IList<XmlDictionaryString> GetNewStrings()
            {
                return _newStrings;
            }

            public void ClearNewStrings()
            {
                _newStrings = null;
            }
        }
    }

    internal class BinaryFormatBuilder
    {
        private readonly List<byte> _bytes;

        public BinaryFormatBuilder()
        {
            _bytes = new List<byte>();
        }

        public int Count
        {
            get { return _bytes.Count; }
        }

        public void AppendPrefixDictionaryElement(char prefix, int key)
        {
            AppendNode(XmlBinaryNodeType.PrefixDictionaryElementA + GetPrefixOffset(prefix));
            AppendKey(key);
        }

        public void AppendDictionaryXmlnsAttribute(char prefix, int key)
        {
            AppendNode(XmlBinaryNodeType.DictionaryXmlnsAttribute);
            AppendUtf8(prefix);
            AppendKey(key);
        }

        public void AppendPrefixDictionaryAttribute(char prefix, int key, char value)
        {
            AppendNode(XmlBinaryNodeType.PrefixDictionaryAttributeA + GetPrefixOffset(prefix));
            AppendKey(key);
            if (value == '1')
            {
                AppendNode(XmlBinaryNodeType.OneText);
            }
            else
            {
                AppendNode(XmlBinaryNodeType.Chars8Text);
                AppendUtf8(value);
            }
        }

        public void AppendDictionaryAttribute(char prefix, int key, char value)
        {
            AppendNode(XmlBinaryNodeType.DictionaryAttribute);
            AppendUtf8(prefix);
            AppendKey(key);
            AppendNode(XmlBinaryNodeType.Chars8Text);
            AppendUtf8(value);
        }

        public void AppendDictionaryTextWithEndElement(int key)
        {
            AppendNode(XmlBinaryNodeType.DictionaryTextWithEndElement);
            AppendKey(key);
        }

        public void AppendDictionaryTextWithEndElement()
        {
            AppendNode(XmlBinaryNodeType.DictionaryTextWithEndElement);
        }

        public void AppendUniqueIDWithEndElement()
        {
            AppendNode(XmlBinaryNodeType.UniqueIdTextWithEndElement);
        }

        public void AppendEndElement()
        {
            AppendNode(XmlBinaryNodeType.EndElement);
        }

        private void AppendKey(int key)
        {
            if (key < 0 || key >= 0x4000)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(key), key,
                    SR.Format(SR.ValueMustBeInRange, 0, 0x4000)));
            }
            if (key >= 0x80)
            {
                AppendByte((key & 0x7f) | 0x80);
                AppendByte(key >> 7);
            }
            else
            {
                AppendByte(key);
            }
        }

        private void AppendNode(XmlBinaryNodeType value)
        {
            AppendByte((int)value);
        }

        private void AppendByte(int value)
        {
            if (value < 0 || value > 0xFF)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(value), value,
                    SR.Format(SR.ValueMustBeInRange, 0, 0xFF)));
            }
            _bytes.Add((byte)value);
        }

        private void AppendUtf8(char value)
        {
            AppendByte(1);
            AppendByte((int)value);
        }

        public int GetStaticKey(int value)
        {
            return value * 2;
        }

        public int GetSessionKey(int value)
        {
            return value * 2 + 1;
        }

        private int GetPrefixOffset(char prefix)
        {
            if (prefix < 'a' || prefix > 'z')
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(prefix), prefix,
                    SR.Format(SR.ValueMustBeInRange, 'a', 'z')));
            }
            return prefix - 'a';
        }

        public byte[] ToByteArray()
        {
            byte[] array = _bytes.ToArray();
            _bytes.Clear();
            return array;
        }
    }

    internal static class BinaryFormatParser
    {
        public static bool IsSessionKey(int value)
        {
            return (value & 1) != 0;
        }

        public static int GetSessionKey(int value)
        {
            return value / 2;
        }

        public static int GetStaticKey(int value)
        {
            return value / 2;
        }

        public static int ParseInt32(byte[] buffer, int offset, int size)
        {
            switch (size)
            {
                case 1:
                    return buffer[offset];
                case 2:
                    return (buffer[offset] & 0x7f) + (buffer[offset + 1] << 7);
                case 3:
                    return (buffer[offset] & 0x7f) + ((buffer[offset + 1] & 0x7f) << 7) + (buffer[offset + 2] << 14);
                case 4:
                    return (buffer[offset] & 0x7f) + ((buffer[offset + 1] & 0x7f) << 7) + ((buffer[offset + 2] & 0x7f) << 14) + (buffer[offset + 3] << 21);
                default:
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(size), size,
                        SR.Format(SR.ValueMustBeInRange, 1, 4)));
            }
        }

        public static int ParseKey(byte[] buffer, int offset, int size)
        {
            return ParseInt32(buffer, offset, size);
        }

        public static unsafe UniqueId ParseUniqueID(byte[] buffer, int offset, int size)
        {
            return new UniqueId(buffer, offset);
        }

        public static int MatchBytes(byte[] buffer, int offset, int size, byte[] buffer2)
        {
            if (size < buffer2.Length)
            {
                return 0;
            }
            int j = offset;
            for (int i = 0; i < buffer2.Length; i++, j++)
            {
                if (buffer2[i] != buffer[j])
                {
                    return 0;
                }
            }
            return buffer2.Length;
        }


        public static bool MatchAttributeNode(byte[] buffer, int offset, int size)
        {
            const XmlBinaryNodeType minAttribute = XmlBinaryNodeType.ShortAttribute;
            const XmlBinaryNodeType maxAttribute = XmlBinaryNodeType.DictionaryAttribute;
            if (size < 1)
            {
                return false;
            }
            XmlBinaryNodeType nodeType = (XmlBinaryNodeType)buffer[offset];
            return nodeType >= minAttribute && nodeType <= maxAttribute;
        }

        public static int MatchKey(byte[] buffer, int offset, int size)
        {
            return MatchInt32(buffer, offset, size);
        }

        public static int MatchInt32(byte[] buffer, int offset, int size)
        {
            if (size > 0)
            {
                if ((buffer[offset] & 0x80) == 0)
                {
                    return 1;
                }
            }
            if (size > 1)
            {
                if ((buffer[offset + 1] & 0x80) == 0)
                {
                    return 2;
                }
            }
            if (size > 2)
            {
                if ((buffer[offset + 2] & 0x80) == 0)
                {
                    return 3;
                }
            }
            if (size > 3)
            {
                if ((buffer[offset + 3] & 0x80) == 0)
                {
                    return 4;
                }
            }

            return 0;
        }

        public static int MatchUniqueID(byte[] buffer, int offset, int size)
        {
            if (size < 16)
            {
                return 0;
            }
            return 16;
        }
    }

    internal class MessagePatterns
    {
        private static readonly byte[] s_commonFragment; // <Envelope><Headers><Action>
        private static readonly byte[] s_requestFragment1; // </Action><MessageID>
        private static readonly byte[] s_requestFragment2; // </MessageID><ReplyTo>...</ReplyTo><To>session-to-key</To></Headers><Body>
        private static readonly byte[] s_responseFragment1; // </Action><RelatesTo>
        private static readonly byte[] s_responseFragment2; // </RelatesTo><To>static-anonymous-key</To></Headers><Body>
        private static readonly byte[] s_bodyFragment; // <Envelope><Body>
        private const int ToValueSessionKey = 1;
        private readonly IXmlDictionary _dictionary;
        private readonly XmlBinaryReaderSession _readerSession;
        private ToHeader _toHeader;
        private readonly MessageVersion _messageVersion;

        static MessagePatterns()
        {
            BinaryFormatBuilder builder = new BinaryFormatBuilder();

            MessageDictionary messageDictionary = XD.MessageDictionary;
            Message12Dictionary message12Dictionary = XD.Message12Dictionary;
            AddressingDictionary addressingDictionary = XD.AddressingDictionary;
            Addressing10Dictionary addressing10Dictionary = XD.Addressing10Dictionary;

            char messagePrefix = MessageStrings.Prefix[0];
            char addressingPrefix = AddressingStrings.Prefix[0];

            // <s:Envelope xmlns:s="soap-ns" xmlns="addressing-ns">
            builder.AppendPrefixDictionaryElement(messagePrefix, builder.GetStaticKey(messageDictionary.Envelope.Key));
            builder.AppendDictionaryXmlnsAttribute(messagePrefix, builder.GetStaticKey(message12Dictionary.Namespace.Key));
            builder.AppendDictionaryXmlnsAttribute(addressingPrefix, builder.GetStaticKey(addressing10Dictionary.Namespace.Key));

            // <s:Header>
            builder.AppendPrefixDictionaryElement(messagePrefix, builder.GetStaticKey(messageDictionary.Header.Key));

            // <a:Action>...
            builder.AppendPrefixDictionaryElement(addressingPrefix, builder.GetStaticKey(addressingDictionary.Action.Key));
            builder.AppendPrefixDictionaryAttribute(messagePrefix, builder.GetStaticKey(messageDictionary.MustUnderstand.Key), '1');
            builder.AppendDictionaryTextWithEndElement();
            s_commonFragment = builder.ToByteArray();

            // <a:MessageID>...
            builder.AppendPrefixDictionaryElement(addressingPrefix, builder.GetStaticKey(addressingDictionary.MessageId.Key));
            builder.AppendUniqueIDWithEndElement();
            s_requestFragment1 = builder.ToByteArray();

            // <a:ReplyTo><a:Address>static-anonymous-key</a:Address></a:ReplyTo>
            builder.AppendPrefixDictionaryElement(addressingPrefix, builder.GetStaticKey(addressingDictionary.ReplyTo.Key));
            builder.AppendPrefixDictionaryElement(addressingPrefix, builder.GetStaticKey(addressingDictionary.Address.Key));
            builder.AppendDictionaryTextWithEndElement(builder.GetStaticKey(addressing10Dictionary.Anonymous.Key));
            builder.AppendEndElement();

            // <a:To>session-to-key</a:To>
            builder.AppendPrefixDictionaryElement(addressingPrefix, builder.GetStaticKey(addressingDictionary.To.Key));
            builder.AppendPrefixDictionaryAttribute(messagePrefix, builder.GetStaticKey(messageDictionary.MustUnderstand.Key), '1');
            builder.AppendDictionaryTextWithEndElement(builder.GetSessionKey(ToValueSessionKey));

            // </s:Header>
            builder.AppendEndElement();

            // <s:Body>
            builder.AppendPrefixDictionaryElement(messagePrefix, builder.GetStaticKey(messageDictionary.Body.Key));
            s_requestFragment2 = builder.ToByteArray();

            // <a:RelatesTo>...
            builder.AppendPrefixDictionaryElement(addressingPrefix, builder.GetStaticKey(addressingDictionary.RelatesTo.Key));
            builder.AppendUniqueIDWithEndElement();
            s_responseFragment1 = builder.ToByteArray();

            // <a:To>static-anonymous-key</a:To>
            builder.AppendPrefixDictionaryElement(addressingPrefix, builder.GetStaticKey(addressingDictionary.To.Key));
            builder.AppendPrefixDictionaryAttribute(messagePrefix, builder.GetStaticKey(messageDictionary.MustUnderstand.Key), '1');
            builder.AppendDictionaryTextWithEndElement(builder.GetStaticKey(addressing10Dictionary.Anonymous.Key));

            // </s:Header>
            builder.AppendEndElement();

            // <s:Body>
            builder.AppendPrefixDictionaryElement(messagePrefix, builder.GetStaticKey(messageDictionary.Body.Key));
            s_responseFragment2 = builder.ToByteArray();

            // <s:Envelope xmlns:s="soap-ns" xmlns="addressing-ns">
            builder.AppendPrefixDictionaryElement(messagePrefix, builder.GetStaticKey(messageDictionary.Envelope.Key));
            builder.AppendDictionaryXmlnsAttribute(messagePrefix, builder.GetStaticKey(message12Dictionary.Namespace.Key));
            builder.AppendDictionaryXmlnsAttribute(addressingPrefix, builder.GetStaticKey(addressing10Dictionary.Namespace.Key));

            // <s:Body>
            builder.AppendPrefixDictionaryElement(messagePrefix, builder.GetStaticKey(messageDictionary.Body.Key));
            s_bodyFragment = builder.ToByteArray();
        }

        public MessagePatterns(IXmlDictionary dictionary, XmlBinaryReaderSession readerSession, MessageVersion messageVersion)
        {
            _dictionary = dictionary;
            _readerSession = readerSession;
            _messageVersion = messageVersion;
        }

        public Message TryCreateMessage(byte[] buffer, int offset, int size, BufferManager bufferManager, BufferedMessageData messageData)
        {
            RelatesToHeader relatesToHeader;
            MessageIDHeader messageIDHeader;
            XmlDictionaryString toString;

            int currentOffset = offset;
            int remainingSize = size;

            int bytesMatched = BinaryFormatParser.MatchBytes(buffer, currentOffset, remainingSize, s_commonFragment);
            if (bytesMatched == 0)
            {
                return null;
            }
            currentOffset += bytesMatched;
            remainingSize -= bytesMatched;

            bytesMatched = BinaryFormatParser.MatchKey(buffer, currentOffset, remainingSize);
            if (bytesMatched == 0)
            {
                return null;
            }
            int actionOffset = currentOffset;
            int actionSize = bytesMatched;
            currentOffset += bytesMatched;
            remainingSize -= bytesMatched;

            int totalBytesMatched;

            bytesMatched = BinaryFormatParser.MatchBytes(buffer, currentOffset, remainingSize, s_requestFragment1);
            if (bytesMatched != 0)
            {
                currentOffset += bytesMatched;
                remainingSize -= bytesMatched;

                bytesMatched = BinaryFormatParser.MatchUniqueID(buffer, currentOffset, remainingSize);
                if (bytesMatched == 0)
                {
                    return null;
                }
                int messageIDOffset = currentOffset;
                int messageIDSize = bytesMatched;
                currentOffset += bytesMatched;
                remainingSize -= bytesMatched;

                bytesMatched = BinaryFormatParser.MatchBytes(buffer, currentOffset, remainingSize, s_requestFragment2);
                if (bytesMatched == 0)
                {
                    return null;
                }
                currentOffset += bytesMatched;
                remainingSize -= bytesMatched;

                if (BinaryFormatParser.MatchAttributeNode(buffer, currentOffset, remainingSize))
                {
                    return null;
                }

                UniqueId messageId = BinaryFormatParser.ParseUniqueID(buffer, messageIDOffset, messageIDSize);
                messageIDHeader = MessageIDHeader.Create(messageId, _messageVersion.Addressing);
                relatesToHeader = null;

                if (!_readerSession.TryLookup(ToValueSessionKey, out toString))
                {
                    return null;
                }

                totalBytesMatched = s_requestFragment1.Length + messageIDSize + s_requestFragment2.Length;
            }
            else
            {
                bytesMatched = BinaryFormatParser.MatchBytes(buffer, currentOffset, remainingSize, s_responseFragment1);

                if (bytesMatched == 0)
                {
                    return null;
                }

                currentOffset += bytesMatched;
                remainingSize -= bytesMatched;

                bytesMatched = BinaryFormatParser.MatchUniqueID(buffer, currentOffset, remainingSize);
                if (bytesMatched == 0)
                {
                    return null;
                }
                int messageIDOffset = currentOffset;
                int messageIDSize = bytesMatched;
                currentOffset += bytesMatched;
                remainingSize -= bytesMatched;

                bytesMatched = BinaryFormatParser.MatchBytes(buffer, currentOffset, remainingSize, s_responseFragment2);
                if (bytesMatched == 0)
                {
                    return null;
                }
                currentOffset += bytesMatched;
                remainingSize -= bytesMatched;

                if (BinaryFormatParser.MatchAttributeNode(buffer, currentOffset, remainingSize))
                {
                    return null;
                }

                UniqueId messageId = BinaryFormatParser.ParseUniqueID(buffer, messageIDOffset, messageIDSize);
                relatesToHeader = RelatesToHeader.Create(messageId, _messageVersion.Addressing);
                messageIDHeader = null;
                toString = XD.Addressing10Dictionary.Anonymous;

                totalBytesMatched = s_responseFragment1.Length + messageIDSize + s_responseFragment2.Length;
            }

            totalBytesMatched += s_commonFragment.Length + actionSize;

            int actionKey = BinaryFormatParser.ParseKey(buffer, actionOffset, actionSize);

            if (!TryLookupKey(actionKey, out XmlDictionaryString actionString))
            {
                return null;
            }

            ActionHeader actionHeader = ActionHeader.Create(actionString, _messageVersion.Addressing);

            if (_toHeader == null)
            {
                _toHeader = ToHeader.Create(new Uri(toString.Value), _messageVersion.Addressing);
            }

            int abandonedSize = totalBytesMatched - s_bodyFragment.Length;

            offset += abandonedSize;
            size -= abandonedSize;

            Buffer.BlockCopy(s_bodyFragment, 0, buffer, offset, s_bodyFragment.Length);

            messageData.Open(new ArraySegment<byte>(buffer, offset, size), bufferManager);

            PatternMessage patternMessage = new PatternMessage(messageData, _messageVersion);

            MessageHeaders headers = patternMessage.Headers;
            headers.AddActionHeader(actionHeader);
            if (messageIDHeader != null)
            {
                headers.AddMessageIDHeader(messageIDHeader);
                headers.AddReplyToHeader(ReplyToHeader.AnonymousReplyTo10);
            }
            else
            {
                headers.AddRelatesToHeader(relatesToHeader);
            }
            headers.AddToHeader(_toHeader);

            return patternMessage;
        }

        private bool TryLookupKey(int key, out XmlDictionaryString result)
        {
            if (BinaryFormatParser.IsSessionKey(key))
            {
                return _readerSession.TryLookup(BinaryFormatParser.GetSessionKey(key), out result);
            }
            else
            {
                return _dictionary.TryLookup(BinaryFormatParser.GetStaticKey(key), out result);
            }
        }

        private sealed class PatternMessage : ReceivedMessage
        {
            private IBufferedMessageData _messageData;
            private readonly MessageHeaders _headers;
            private RecycledMessageState _recycledMessageState;
            private readonly MessageProperties _properties;
            private XmlDictionaryReader _reader;

            public PatternMessage(IBufferedMessageData messageData, MessageVersion messageVersion)
            {
                _messageData = messageData;
                _recycledMessageState = messageData.TakeMessageState();
                if (_recycledMessageState == null)
                {
                    _recycledMessageState = new RecycledMessageState();
                }
                _properties = _recycledMessageState.TakeProperties();
                if (_properties == null)
                {
                    _properties = new MessageProperties();
                }
                _headers = _recycledMessageState.TakeHeaders();
                if (_headers == null)
                {
                    _headers = new MessageHeaders(messageVersion);
                }
                else
                {
                    _headers.Init(messageVersion);
                }
                XmlDictionaryReader reader = messageData.GetMessageReader();
                reader.ReadStartElement();
                VerifyStartBody(reader, messageVersion.Envelope);
                ReadStartBody(reader);
                _reader = reader;
            }

            public PatternMessage(IBufferedMessageData messageData, MessageVersion messageVersion,
                KeyValuePair<string, object>[] properties, MessageHeaders headers)
            {
                _messageData = messageData;
                _messageData.Open();
                _recycledMessageState = _messageData.TakeMessageState();
                if (_recycledMessageState == null)
                {
                    _recycledMessageState = new RecycledMessageState();
                }

                _properties = _recycledMessageState.TakeProperties();
                if (_properties == null)
                {
                    _properties = new MessageProperties();
                }
                if (properties != null)
                {
                    _properties.CopyProperties(properties);
                }

                _headers = _recycledMessageState.TakeHeaders();
                if (_headers == null)
                {
                    _headers = new MessageHeaders(messageVersion);
                }
                if (headers != null)
                {
                    _headers.CopyHeadersFrom(headers);
                }

                XmlDictionaryReader reader = messageData.GetMessageReader();
                reader.ReadStartElement();
                VerifyStartBody(reader, messageVersion.Envelope);
                ReadStartBody(reader);
                _reader = reader;
            }


            public override MessageHeaders Headers
            {
                get
                {
                    if (IsDisposed)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateMessageDisposedException());
                    }
                    return _headers;
                }
            }

            public override MessageProperties Properties
            {
                get
                {
                    if (IsDisposed)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateMessageDisposedException());
                    }
                    return _properties;
                }
            }

            public override MessageVersion Version
            {
                get
                {
                    if (IsDisposed)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateMessageDisposedException());
                    }
                    return _headers.MessageVersion;
                }
            }

            public override RecycledMessageState RecycledMessageState
            {
                get { return _recycledMessageState; }
            }

            private XmlDictionaryReader GetBufferedReaderAtBody()
            {
                XmlDictionaryReader reader = _messageData.GetMessageReader();
                reader.ReadStartElement();
                reader.ReadStartElement();
                return reader;
            }

            protected override void OnBodyToString(XmlDictionaryWriter writer)
            {
                using (XmlDictionaryReader reader = GetBufferedReaderAtBody())
                {
                    while (reader.NodeType != XmlNodeType.EndElement)
                    {
                        writer.WriteNode(reader, false);
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
                    _messageData.ReturnMessageState(_recycledMessageState);
                    _recycledMessageState = null;
                    _messageData.Close();
                    _messageData = null;
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

            protected override MessageBuffer OnCreateBufferedCopy(int maxBufferSize)
            {
                KeyValuePair<string, object>[] properties = new KeyValuePair<string, object>[Properties.Count];
                ((ICollection<KeyValuePair<string, object>>)Properties).CopyTo(properties, 0);
                _messageData.EnableMultipleUsers();
                return new PatternMessageBuffer(_messageData, Version, properties, _headers);
            }

            protected override XmlDictionaryReader OnGetReaderAtBodyContents()
            {
                XmlDictionaryReader reader = _reader;
                _reader = null;
                return reader;
            }

            protected override string OnGetBodyAttribute(string localName, string ns)
            {
                return null;
            }
        }

        private class PatternMessageBuffer : MessageBuffer
        {
            private bool _closed;
            private MessageHeaders _headers;
            private IBufferedMessageData _messageDataAtBody;
            private MessageVersion _messageVersion;
            private KeyValuePair<string, object>[] _properties;
            private RecycledMessageState _recycledMessageState;

            public PatternMessageBuffer(IBufferedMessageData messageDataAtBody, MessageVersion messageVersion,
                KeyValuePair<string, object>[] properties, MessageHeaders headers)
            {
                _messageDataAtBody = messageDataAtBody;
                _messageDataAtBody.Open();

                _recycledMessageState = _messageDataAtBody.TakeMessageState();
                if (_recycledMessageState == null)
                {
                    _recycledMessageState = new RecycledMessageState();
                }

                _headers = _recycledMessageState.TakeHeaders();
                if (_headers == null)
                {
                    _headers = new MessageHeaders(messageVersion);
                }
                _headers.CopyHeadersFrom(headers);
                _properties = properties;
                _messageVersion = messageVersion;
            }

            public override int BufferSize
            {
                get
                {
                    lock (ThisLock)
                    {
                        if (_closed)
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateBufferDisposedException());
                        }

                        return _messageDataAtBody.Buffer.Count;
                    }
                }
            }

            private object ThisLock { get; } = new object();

            public override void Close()
            {
                lock (ThisLock)
                {
                    if (!_closed)
                    {
                        _closed = true;
                        _recycledMessageState.ReturnHeaders(_headers);
                        _messageDataAtBody.ReturnMessageState(_recycledMessageState);
                        _messageDataAtBody.Close();
                        _recycledMessageState = null;
                        _messageDataAtBody = null;
                        _properties = null;
                        _messageVersion = null;
                        _headers = null;
                    }
                }
            }

            public override Message CreateMessage()
            {
                lock (ThisLock)
                {
                    if (_closed)
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(CreateBufferDisposedException());
                    }

                    return new PatternMessage(_messageDataAtBody, _messageVersion, _properties,
                        _headers);
                }
            }
        }
    }
}
