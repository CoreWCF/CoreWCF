// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

using CoreWCF.Description;
using CoreWCF.Diagnostics;
using CoreWCF.Runtime;
using CoreWCF.Runtime.Diagnostics;
using CoreWCF.Xml;

namespace CoreWCF.Channels
{
    internal class MtomMessageEncoderFactory : MessageEncoderFactory
    {
        private MessageVersion _messageVersion;
        private Encoding _writeEncoding;
        private int _maxReadPoolSize;
        private int _maxWritePoolSize;
        private int _maxBufferSize;
        private XmlDictionaryReaderQuotas _readerQuotas;

        // Pools used by MtomMessageEncoder
        private const int MaxPooledXmlReadersPerMessage = 2;
        private object _thisLock;
        private OnXmlDictionaryReaderClose _onStreamedReaderClose;
        // Double-checked locking pattern requires volatile for read/write synchronization
        private volatile SynchronizedPool<XmlDictionaryWriter> _streamedWriterPool;
        private volatile SynchronizedPool<XmlDictionaryReader> _streamedReaderPool;
        private volatile SynchronizedPool<MtomMessageEncoder.MtomBufferedMessageData> _bufferedReaderPool;
        private volatile SynchronizedPool<MtomMessageEncoder.MtomBufferedMessageWriter> _bufferedWriterPool;
        private volatile SynchronizedPool<RecycledMessageState> _recycledStatePool;

        public MtomMessageEncoderFactory(MessageVersion version, Encoding writeEncoding, int maxReadPoolSize, int maxWritePoolSize, int maxBufferSize, XmlDictionaryReaderQuotas quotas)
        {
            _messageVersion = version;
            _writeEncoding = writeEncoding;
            _maxReadPoolSize = maxReadPoolSize;
            _maxWritePoolSize = maxWritePoolSize;
            _maxBufferSize = maxBufferSize;
            _readerQuotas = quotas;
            _thisLock = new object();
            _onStreamedReaderClose = new OnXmlDictionaryReaderClose(ReturnStreamedReader);
            if (version.Envelope == EnvelopeVersion.Soap12)
            {
                ContentEncodingMap = TextMessageEncoderFactory.Soap12Content;
            }
            else if (version.Envelope == EnvelopeVersion.Soap11)
            {
                ContentEncodingMap = TextMessageEncoderFactory.Soap11Content;
            }
            else
            {
                Fx.Assert("Invalid MessageVersion");
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(string.Format(CultureInfo.InvariantCulture, "Invalid MessageVersion")));
            }
        }

        public override MessageEncoder Encoder => new MtomMessageEncoder(_messageVersion, _writeEncoding, _maxReadPoolSize, _maxWritePoolSize, _maxBufferSize, _readerQuotas, this);

        public override MessageVersion MessageVersion => _messageVersion;

        public int MaxWritePoolSize => _maxWritePoolSize;

        public int MaxReadPoolSize => _maxReadPoolSize;

        public XmlDictionaryReaderQuotas ReaderQuotas => _readerQuotas;

        public int MaxBufferSize => _maxBufferSize;

        internal ContentEncoding[] ContentEncodingMap { get; }

        internal XmlDictionaryWriter TakeStreamedWriter(Stream stream, string startInfo, string boundary, string startUri, bool writeMessageHeaders)
        {
            if (_streamedWriterPool == null)
            {
                lock (_thisLock)
                {
                    if (_streamedWriterPool == null)
                    {
                        _streamedWriterPool = new SynchronizedPool<XmlDictionaryWriter>(MaxWritePoolSize);
                    }
                }
            }
            XmlDictionaryWriter xmlWriter = _streamedWriterPool.Take();
            if (xmlWriter == null)
            {
                xmlWriter = XmlMtomWriter.Create(stream, _writeEncoding, int.MaxValue, startInfo, boundary, startUri, writeMessageHeaders, false);
                // if (WcfEventSource.Instance.WritePoolMissIsEnabled())
                // {
                //     WcfEventSource.Instance.WritePoolMiss(xmlWriter.GetType().Name);
                // }
            }
            else
            {
                ((IXmlMtomWriterInitializer)xmlWriter).SetOutput(stream, _writeEncoding, int.MaxValue, startInfo, boundary, startUri, writeMessageHeaders, false);
            }
            return xmlWriter;
        }

        internal void ReturnStreamedWriter(XmlDictionaryWriter xmlWriter)
        {
            xmlWriter.Close();
            _streamedWriterPool.Return(xmlWriter);
        }

        internal MtomMessageEncoder.MtomBufferedMessageWriter TakeBufferedWriter(MtomMessageEncoder messageEncoder)
        {
            if (_bufferedWriterPool == null)
            {
                lock (_thisLock)
                {
                    if (_bufferedWriterPool == null)
                    {
                        _bufferedWriterPool = new SynchronizedPool<MtomMessageEncoder.MtomBufferedMessageWriter>(MaxWritePoolSize);
                    }
                }
            }

            MtomMessageEncoder.MtomBufferedMessageWriter messageWriter = _bufferedWriterPool.Take();
            if (messageWriter == null)
            {
                messageWriter = new MtomMessageEncoder.MtomBufferedMessageWriter(messageEncoder);
                // if (WcfEventSource.Instance.WritePoolMissIsEnabled())
                // {
                //     WcfEventSource.Instance.WritePoolMiss(messageWriter.GetType().Name);
                // }
            }
            return messageWriter;
        }

        internal void ReturnMessageWriter(MtomMessageEncoder.MtomBufferedMessageWriter messageWriter)
        {
            _bufferedWriterPool.Return(messageWriter);
        }

        internal MtomMessageEncoder.MtomBufferedMessageData TakeBufferedReader(MtomMessageEncoder messageEncoder)
        {
            if (_bufferedReaderPool == null)
            {
                lock (_thisLock)
                {
                    if (_bufferedReaderPool == null)
                    {
                        _bufferedReaderPool = new SynchronizedPool<MtomMessageEncoder.MtomBufferedMessageData>(MaxReadPoolSize);
                    }
                }
            }
            MtomMessageEncoder.MtomBufferedMessageData messageData = _bufferedReaderPool.Take();
            if (messageData == null)
            {
                messageData = new MtomMessageEncoder.MtomBufferedMessageData(messageEncoder, MaxPooledXmlReadersPerMessage);
                // if (WcfEventSource.Instance.ReadPoolMissIsEnabled())
                // {
                //     WcfEventSource.Instance.ReadPoolMiss(messageData.GetType().Name);
                // }
            }
            return messageData;
        }

        internal void ReturnBufferedData(MtomMessageEncoder.MtomBufferedMessageData messageData)
        {
            _bufferedReaderPool.Return(messageData);
        }

        internal XmlReader TakeStreamedReader(Stream stream, string contentType, bool isMtomContentType)
        {
            if (_streamedReaderPool == null)
            {
                lock (_thisLock)
                {
                    if (_streamedReaderPool == null)
                    {
                        _streamedReaderPool = new SynchronizedPool<XmlDictionaryReader>(MaxReadPoolSize);
                    }
                }
            }
            XmlDictionaryReader xmlReader = _streamedReaderPool.Take();
            try
            {
                if (contentType == null || isMtomContentType)
                {
                    if (xmlReader != null && xmlReader is IXmlMtomReaderInitializer)
                    {
                        ((IXmlMtomReaderInitializer)xmlReader).SetInput(stream, MtomMessageEncoderFactory.GetSupportedEncodings(), contentType, ReaderQuotas, MaxBufferSize, _onStreamedReaderClose);
                    }
                    else
                    {
                        xmlReader = XmlMtomReader.Create(stream, MtomMessageEncoderFactory.GetSupportedEncodings(), contentType, ReaderQuotas, MaxBufferSize, _onStreamedReaderClose);
                        // if (WcfEventSource.Instance.ReadPoolMissIsEnabled())
                        // {
                        //     WcfEventSource.Instance.ReadPoolMiss(xmlReader.GetType().Name);
                        // }
                    }
                }
                else
                {
                    if (xmlReader != null && xmlReader is IXmlTextReaderInitializer)
                    {
                        ((IXmlTextReaderInitializer)xmlReader).SetInput(stream, TextMessageEncoderFactory.GetEncodingFromContentType(contentType, ContentEncodingMap), ReaderQuotas, _onStreamedReaderClose);
                    }
                    else
                    {
                        xmlReader = XmlDictionaryReader.CreateTextReader(stream, TextMessageEncoderFactory.GetEncodingFromContentType(contentType, ContentEncodingMap), ReaderQuotas, _onStreamedReaderClose);
                        // if (WcfEventSource.Instance.ReadPoolMissIsEnabled())
                        // {
                        //     WcfEventSource.Instance.ReadPoolMiss(xmlReader.GetType().Name);
                        // }
                    }
                }
            }
            catch (FormatException fe)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CommunicationException(
                    SR.SFxErrorCreatingMtomReader, fe));
            }
            catch (XmlException xe)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CommunicationException(
                    SR.SFxErrorCreatingMtomReader, xe));
            }

            return xmlReader;
        }

        internal void ReturnStreamedReader(XmlDictionaryReader xmlReader)
        {
            _streamedReaderPool.Return(xmlReader);
        }

        internal SynchronizedPool<RecycledMessageState> RecycledStatePool
        {
            get
            {
                if (_recycledStatePool == null)
                {
                    lock (_thisLock)
                    {
                        if (_recycledStatePool == null)
                        {
                            _recycledStatePool = new SynchronizedPool<RecycledMessageState>(MaxReadPoolSize);
                        }
                    }
                }
                return _recycledStatePool;
            }
        }

        public static Encoding[] GetSupportedEncodings()
        {
            Encoding[] supported = TextEncoderDefaults.SupportedEncodings;
            Encoding[] enc = new Encoding[supported.Length];
            Array.Copy(supported, enc, supported.Length);
            return enc;
        }

    }

    // Some notes:
    // The Encoding passed in is used for the SOAP envelope
    internal class MtomMessageEncoder : MessageEncoder, ITraceSourceStringProvider
    {
        private Encoding _writeEncoding;
        private string _contentType;
        private string _boundary;
        private MessageVersion _version;
        private static UriGenerator s_mimeBoundaryGenerator;
        private XmlDictionaryReaderQuotas _bufferedReadReaderQuotas;

        private MtomMessageEncoderFactory _factory;
        private const string MtomMediaType = "multipart/related";
        private const string MtomContentType = MtomMediaType + "; type=\"application/xop+xml\"";
        private const string MtomStartUri = NamingHelper.DefaultNamespace + "0";

        public MtomMessageEncoder(MessageVersion version, Encoding writeEncoding, int maxReadPoolSize, int maxWritePoolSize, int maxBufferSize, XmlDictionaryReaderQuotas quotas, MtomMessageEncoderFactory factory)
        {
            if (version == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(version));
            if (writeEncoding == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(writeEncoding));

            _factory = factory;
            TextEncoderDefaults.ValidateEncoding(writeEncoding);
            _writeEncoding = writeEncoding;

            MaxReadPoolSize = maxReadPoolSize;
            MaxWritePoolSize = maxWritePoolSize;

            ReaderQuotas = new XmlDictionaryReaderQuotas();
            quotas.CopyTo(ReaderQuotas);

            _bufferedReadReaderQuotas = EncoderHelpers.GetBufferedReadQuotas(ReaderQuotas);
            MaxBufferSize = maxBufferSize;
            _version = version;
            _contentType = GetContentType(out _boundary);
        }

        private static UriGenerator MimeBoundaryGenerator
        {
            get
            {
                if (s_mimeBoundaryGenerator == null)
                    s_mimeBoundaryGenerator = new UriGenerator("uuid", "+");
                return s_mimeBoundaryGenerator;
            }
        }

        public override string ContentType => _contentType;

        public int MaxWritePoolSize { get; }

        public int MaxReadPoolSize { get; }

        public XmlDictionaryReaderQuotas ReaderQuotas { get; }

        public int MaxBufferSize { get; }

        public override string MediaType => MtomMediaType;

        public override MessageVersion MessageVersion => _version;

        internal bool IsMTOMContentType(string contentType)
        {
            // check for MTOM contentType: multipart/related; type=\"application/xop+xml\"
            return IsContentTypeSupported(contentType, ContentType, MediaType);
        }

        internal bool IsTextContentType(string contentType)
        {
            // check for Text contentType: text/xml or application/soap+xml
            string textMediaType = TextMessageEncoderFactory.GetMediaType(_version);
            string textContentType = TextMessageEncoderFactory.GetContentType(textMediaType, _writeEncoding);
            return IsContentTypeSupported(contentType, textContentType, textMediaType);
        }

        public override bool IsContentTypeSupported(string contentType)
        {
            if (contentType == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(contentType)));
            return (IsMTOMContentType(contentType) || IsTextContentType(contentType));
        }

        protected override bool IsCharSetSupported(string charSet)
        {
            if (charSet == null || charSet.Length == 0)
                return true;

            Encoding tmp;
            return TextEncoderDefaults.TryGetEncoding(charSet, out tmp);
        }

        private string GenerateStartInfoString()
        {
            return (_version.Envelope == EnvelopeVersion.Soap12) ? TextMessageEncoderFactory.Soap12MediaType : TextMessageEncoderFactory.Soap11MediaType;
        }

        public override Message ReadMessage(ArraySegment<byte> buffer, BufferManager bufferManager, string contentType)
        {
            if (bufferManager == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(bufferManager));

            if (contentType == ContentType)
                contentType = null;

            // if (WcfEventSource.Instance.MtomMessageDecodingStartIsEnabled())
            // {
            //     WcfEventSource.Instance.MtomMessageDecodingStart();
            // }

            MtomBufferedMessageData messageData = _factory.TakeBufferedReader(this);
            messageData._contentType = contentType;
            messageData.Open(buffer, bufferManager);
            RecycledMessageState messageState = messageData.TakeMessageState();
            if (messageState == null)
                messageState = new RecycledMessageState();
            Message message = new BufferedMessage(messageData, messageState);
            message.Properties.Encoder = this;
            // if (MessageLogger.LogMessagesAtTransportLevel)
            //     MessageLogger.LogMessage(ref message, MessageLoggingSource.TransportReceive);
            //
            // if (WcfEventSource.Instance.MessageReadByEncoderIsEnabled() && buffer != null)
            // {
            //     WcfEventSource.Instance.MessageReadByEncoder(
            //         EventTraceActivityHelper.TryExtractActivity(message, true),
            //         buffer.Count,
            //         this);
            // }

            return message;
        }

        public override Task<Message> ReadMessageAsync(Stream stream, int maxSizeOfHeaders, string contentType)
        {
            if (stream == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(stream)));

            if (contentType == ContentType)
                contentType = null;

            // if (WcfEventSource.Instance.MtomMessageDecodingStartIsEnabled())
            // {
            //     WcfEventSource.Instance.MtomMessageDecodingStart();
            // }

            XmlReader reader = _factory.TakeStreamedReader(stream, contentType, contentType == null || IsMTOMContentType(contentType));
            Message message = Message.CreateMessage(reader, maxSizeOfHeaders, _version);
            message.Properties.Encoder = this;

            // if (WcfEventSource.Instance.StreamedMessageReadByEncoderIsEnabled())
            // {
            //     WcfEventSource.Instance.StreamedMessageReadByEncoder(EventTraceActivityHelper.TryExtractActivity(message, true));
            // }
            //
            // if (MessageLogger.LogMessagesAtTransportLevel)
            //     MessageLogger.LogMessage(ref message, MessageLoggingSource.TransportReceive);
            return Task.FromResult(message);
        }

        public override ArraySegment<byte> WriteMessage(Message message, int maxMessageSize, BufferManager bufferManager, int messageOffset)
        {
            return WriteMessageInternal(message, maxMessageSize, bufferManager, messageOffset, GenerateStartInfoString(), _boundary, MtomStartUri);
        }

        // public override Task<ArraySegment<byte>> WriteMessageAsync(Message message, int maxMessageSize, BufferManager bufferManager, int messageOffset)
        // {
        //     return Task.FromResult(WriteMessageInternal(message, maxMessageSize, bufferManager, messageOffset, GenerateStartInfoString(), _boundary, MtomStartUri, writeMessageHeaders: true));
        // }

        private string GetContentType(out string boundary)
        {
            string startInfo = GenerateStartInfoString();
            boundary = MimeBoundaryGenerator.Next();

            return FormatContentType(boundary, startInfo);
        }

        internal string FormatContentType(string boundary, string startInfo)
        {
            return string.Format(CultureInfo.InvariantCulture,
                "{0};start=\"<{1}>\";boundary=\"{2}\";start-info=\"{3}\"",
                MtomContentType, MtomStartUri, boundary, startInfo);
        }

        private ArraySegment<byte> WriteMessageInternal(Message message, int maxMessageSize, BufferManager bufferManager, int messageOffset, string startInfo, string boundary, string startUri)
        {
            bool writeMessageHeaders = true;
            if (message.Properties.TryGetValue("CoreWCF.Channel.MtomMessageEncoder.WriteMessageHeaders", out object boolAsObject) && boolAsObject is bool)
            {
                writeMessageHeaders = (bool)boolAsObject;
            }

            if (message == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));
            if (bufferManager == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(bufferManager));
            if (maxMessageSize < 0)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(maxMessageSize), maxMessageSize,
                                                    SR.ValueMustBeNonNegative));
            if (messageOffset < 0 || messageOffset > maxMessageSize)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(messageOffset), messageOffset,
                                                    SR.Format(SR.ValueMustBeInRange, 0, maxMessageSize)));
            ThrowIfMismatchedMessageVersion(message);

            // EventTraceActivity eventTraceActivity = null;
            // if (WcfEventSource.Instance.MtomMessageEncodingStartIsEnabled())
            // {
            //     eventTraceActivity = EventTraceActivityHelper.TryExtractActivity(message);
            //     WcfEventSource.Instance.MtomMessageEncodingStart(eventTraceActivity);
            // }

            message.Properties.Encoder = this;

            MtomBufferedMessageWriter messageWriter = _factory.TakeBufferedWriter(this);
            messageWriter._startInfo = startInfo;
            messageWriter._boundary = boundary;
            messageWriter._startUri = startUri;
            messageWriter._writeMessageHeaders = writeMessageHeaders;
            messageWriter._maxSizeInBytes = maxMessageSize;
            ArraySegment<byte> messageData = messageWriter.WriteMessage(message, bufferManager, messageOffset, maxMessageSize);
            _factory.ReturnMessageWriter(messageWriter);

            // if (WcfEventSource.Instance.MessageWrittenByEncoderIsEnabled() && messageData != null)
            // {
            //     WcfEventSource.Instance.MessageWrittenByEncoder(
            //         eventTraceActivity ?? EventTraceActivityHelper.TryExtractActivity(message),
            //         messageData.Count,
            //         this);
            // }
            //
            // if (MessageLogger.LogMessagesAtTransportLevel)
            // {
            //     string contentType = null;
            //     if (boundary != null)
            //         contentType = FormatContentType(boundary, startInfo ?? GenerateStartInfoString());
            //
            //     XmlDictionaryReader xmlDictionaryReader = XmlMtomReader.Create(messageData.Array, messageData.Offset, messageData.Count, MtomMessageEncoderFactory.GetSupportedEncodings(), contentType, XmlDictionaryReaderQuotas.Max, int.MaxValue, null);
            //     MessageLogger.LogMessage(ref message, xmlDictionaryReader, MessageLoggingSource.TransportSend);
            // }

            return messageData;
        }

        // public override void WriteMessage(Message message, Stream stream)
        // {
        //     WriteMessageAsync(message, stream).WaitForCompletionNoSpin();
        // }

        public override Task WriteMessageAsync(Message message, Stream stream)
        {
            return WriteMessageInternalAsync(message, stream, GenerateStartInfoString(), _boundary, MtomStartUri);
        }

        // public override IAsyncResult BeginWriteMessage(Message message, Stream stream, AsyncCallback callback, object state)
        // {
        //     return WriteMessageAsync(message, stream).ToApm(callback, state);
        // }
        //
        // public override void EndWriteMessage(IAsyncResult result)
        // {
        //     result.ToApmEnd();
        // }

        private async Task WriteMessageInternalAsync(Message message, Stream stream, string startInfo, string boundary, string startUri)
        {
            bool writeMessageHeaders = true;
            if (message.Properties.TryGetValue("CoreWCF.Channel.MtomMessageEncoder.WriteMessageHeaders", out object boolAsObject) && boolAsObject is bool)
            {
                writeMessageHeaders = (bool)boolAsObject;
            }

            if (message == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(message)));
            if (stream == null)
                throw TraceUtility.ThrowHelperError(new ArgumentNullException(nameof(stream)), message);
            ThrowIfMismatchedMessageVersion(message);

            // EventTraceActivity eventTraceActivity = null;
            // if (WcfEventSource.Instance.MtomMessageEncodingStartIsEnabled())
            // {
            //     eventTraceActivity = EventTraceActivityHelper.TryExtractActivity(message);
            //     WcfEventSource.Instance.MtomMessageEncodingStart(eventTraceActivity);
            // }

            message.Properties.Encoder = this;
            // if (MessageLogger.LogMessagesAtTransportLevel)
            //     MessageLogger.LogMessage(ref message, MessageLoggingSource.TransportSend);
            XmlDictionaryWriter xmlWriter = _factory.TakeStreamedWriter(stream, startInfo, boundary, startUri, writeMessageHeaders);
            if (_writeEncoding.WebName == "utf-8")
            {
                await message.WriteMessageAsync(xmlWriter);
            }
            else
            {
                await xmlWriter.WriteStartDocumentAsync();
                await message.WriteMessageAsync(xmlWriter);
                await xmlWriter.WriteEndDocumentAsync();
            }

            await xmlWriter.FlushAsync();
            _factory.ReturnStreamedWriter(xmlWriter);

            // if (WcfEventSource.Instance.StreamedMessageWrittenByEncoderIsEnabled())
            // {
            //     WcfEventSource.Instance.StreamedMessageWrittenByEncoder(eventTraceActivity ?? EventTraceActivityHelper.TryExtractActivity(message));
            // }
        }

        string ITraceSourceStringProvider.GetSourceString()
        {
            return GetTraceSourceString();
        }

        internal class MtomBufferedMessageData : BufferedMessageData
        {
            private MtomMessageEncoder _messageEncoder;
            private Pool<XmlDictionaryReader> _readerPool;
            internal string _contentType;
            private OnXmlDictionaryReaderClose _onClose;

            public MtomBufferedMessageData(MtomMessageEncoder messageEncoder, int maxReaderPoolSize)
                : base(messageEncoder._factory.RecycledStatePool)
            {
                _messageEncoder = messageEncoder;
                _readerPool = new Pool<XmlDictionaryReader>(maxReaderPoolSize);
                _onClose = new OnXmlDictionaryReaderClose(OnXmlReaderClosed);
            }

            public override MessageEncoder MessageEncoder => _messageEncoder;

            public override XmlDictionaryReaderQuotas Quotas => _messageEncoder._bufferedReadReaderQuotas;

            protected override void OnClosed()
            {
                _messageEncoder._factory.ReturnBufferedData(this);
            }

            protected override XmlDictionaryReader TakeXmlReader()
            {
                try
                {
                    ArraySegment<byte> buffer = Buffer;

                    XmlDictionaryReader xmlReader = _readerPool.Take();
                    if (_contentType == null || _messageEncoder.IsMTOMContentType(_contentType))
                    {
                        if (xmlReader != null && xmlReader is IXmlMtomReaderInitializer)
                        {
                            ((IXmlMtomReaderInitializer)xmlReader).SetInput(buffer.Array, buffer.Offset, buffer.Count, MtomMessageEncoderFactory.GetSupportedEncodings(), _contentType, Quotas, _messageEncoder.MaxBufferSize, _onClose);
                        }
                        else
                        {
                            xmlReader = XmlMtomReader.Create(buffer.Array, buffer.Offset, buffer.Count, MtomMessageEncoderFactory.GetSupportedEncodings(), _contentType, Quotas, _messageEncoder.MaxBufferSize, _onClose);
                            // if (WcfEventSource.Instance.ReadPoolMissIsEnabled())
                            // {
                            //     WcfEventSource.Instance.ReadPoolMiss(xmlReader.GetType().Name);
                            // }
                        }
                    }
                    else
                    {
                        if (xmlReader != null && xmlReader is IXmlTextReaderInitializer)
                        {
                            ((IXmlTextReaderInitializer)xmlReader).SetInput(buffer.Array, buffer.Offset, buffer.Count, TextMessageEncoderFactory.GetEncodingFromContentType(_contentType, _messageEncoder._factory.ContentEncodingMap), Quotas, _onClose);
                        }
                        else
                        {
                            xmlReader = XmlDictionaryReader.CreateTextReader(buffer.Array, buffer.Offset, buffer.Count, TextMessageEncoderFactory.GetEncodingFromContentType(_contentType, _messageEncoder._factory.ContentEncodingMap), Quotas, _onClose);
                            // if (WcfEventSource.Instance.ReadPoolMissIsEnabled())
                            // {
                            //     WcfEventSource.Instance.ReadPoolMiss(xmlReader.GetType().Name);
                            // }
                        }
                    }
                    return xmlReader;
                }
                catch (FormatException fe)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CommunicationException(
                        SR.SFxErrorCreatingMtomReader, fe));
                }
                catch (XmlException xe)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CommunicationException(
                        SR.SFxErrorCreatingMtomReader, xe));
                }
            }

            protected override void ReturnXmlReader(XmlDictionaryReader xmlReader)
            {
                if (xmlReader != null)
                    _readerPool.Return(xmlReader);
            }
        }

        internal class MtomBufferedMessageWriter : BufferedMessageWriter
        {
            private MtomMessageEncoder _messageEncoder;
            internal bool _writeMessageHeaders;
            internal string _startInfo;
            internal string _startUri;
            internal string _boundary;
            internal int _maxSizeInBytes = int.MaxValue;
            private XmlDictionaryWriter _writer;

            public MtomBufferedMessageWriter(MtomMessageEncoder messageEncoder)
            {
                _messageEncoder = messageEncoder;
            }

            protected override XmlDictionaryWriter TakeXmlWriter(Stream stream)
            {
                XmlDictionaryWriter returnedWriter = _writer;
                if (returnedWriter == null)
                {
                    returnedWriter = XmlMtomWriter.Create(stream, _messageEncoder._writeEncoding, _maxSizeInBytes, _startInfo, _boundary, _startUri, _writeMessageHeaders, false);
                }
                else
                {
                    _writer = null;
                    ((IXmlMtomWriterInitializer)returnedWriter).SetOutput(stream, _messageEncoder._writeEncoding, _maxSizeInBytes, _startInfo, _boundary, _startUri, _writeMessageHeaders, false);
                }
                if (_messageEncoder._writeEncoding.WebName != "utf-8")
                    returnedWriter.WriteStartDocument();
                return returnedWriter;
            }

            protected override void ReturnXmlWriter(XmlDictionaryWriter writer)
            {
                writer.Close();

                if (_writer == null)
                    _writer = writer;
            }
        }
    }
}
