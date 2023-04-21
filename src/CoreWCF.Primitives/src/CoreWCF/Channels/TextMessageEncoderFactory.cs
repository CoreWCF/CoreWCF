// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.Diagnostics;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    internal class TextMessageEncoderFactory : MessageEncoderFactory
    {
        private readonly TextMessageEncoder _messageEncoder;
        //internal static ContentEncoding[] Soap11Content = GetContentEncodingMap(MessageVersion.Soap11WSAddressing10);
        // I believe replacing Soap11WSAddressing10 with Soap11 should work
        internal static ContentEncoding[] Soap11Content = GetContentEncodingMap(MessageVersion.Soap11);
        internal static ContentEncoding[] Soap12Content = GetContentEncodingMap(MessageVersion.Soap12WSAddressing10);
        internal static ContentEncoding[] SoapNoneContent = GetContentEncodingMap(MessageVersion.None);
        internal const string Soap11MediaType = "text/xml";
        internal const string Soap12MediaType = "application/soap+xml";
        private const string XmlMediaType = "application/xml";

        public TextMessageEncoderFactory(MessageVersion version, Encoding writeEncoding, int maxReadPoolSize, int maxWritePoolSize, XmlDictionaryReaderQuotas quotas)
        {
            _messageEncoder = new TextMessageEncoder(version, writeEncoding, maxReadPoolSize, maxWritePoolSize, quotas);
        }

        public override MessageEncoder Encoder
        {
            get { return _messageEncoder; }
        }

        public override MessageVersion MessageVersion
        {
            get { return _messageEncoder.MessageVersion; }
        }

        public int MaxWritePoolSize
        {
            get { return _messageEncoder.MaxWritePoolSize; }
        }

        public int MaxReadPoolSize
        {
            get { return _messageEncoder.MaxReadPoolSize; }
        }

        public static Encoding[] GetSupportedEncodings()
        {
            Encoding[] supported = TextEncoderDefaults.SupportedEncodings;
            Encoding[] enc = new Encoding[supported.Length];
            Array.Copy(supported, enc, supported.Length);
            return enc;
        }

        public XmlDictionaryReaderQuotas ReaderQuotas
        {
            get
            {
                return _messageEncoder.ReaderQuotas;
            }
        }

        internal static string GetMediaType(MessageVersion version)
        {
            string mediaType;
            if (version.Envelope == EnvelopeVersion.Soap12)
            {
                mediaType = Soap12MediaType;
            }
            else if (version.Envelope == EnvelopeVersion.Soap11)
            {
                mediaType = Soap11MediaType;
            }
            else if (version.Envelope == EnvelopeVersion.None)
            {
                mediaType = XmlMediaType;
            }
            else
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                    SR.Format(SR.EnvelopeVersionNotSupported, version.Envelope)));
            }
            return mediaType;
        }

        internal static string GetContentType(string mediaType, Encoding encoding)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}; charset={1}", mediaType, TextEncoderDefaults.EncodingToCharSet(encoding));
        }

        private static ContentEncoding[] GetContentEncodingMap(MessageVersion version)
        {
            Encoding[] readEncodings = GetSupportedEncodings();
            string media = GetMediaType(version);
            ContentEncoding[] map = new ContentEncoding[readEncodings.Length];
            for (int i = 0; i < readEncodings.Length; i++)
            {
                ContentEncoding contentEncoding = new ContentEncoding
                {
                    contentType = GetContentType(media, readEncodings[i]),
                    encoding = readEncodings[i]
                };
                map[i] = contentEncoding;
            }
            return map;
        }

        internal static Encoding GetEncodingFromContentType(string contentType, ContentEncoding[] contentMap)
        {
            if (contentType == null)
            {
                return null;
            }

            // Check for known/expected content types
            for (int i = 0; i < contentMap.Length; i++)
            {
                if (contentMap[i].contentType == contentType)
                {
                    return contentMap[i].encoding;
                }
            }

            // then some heuristic matches (since System.Mime.ContentType is a performance hit)
            // start by looking for a parameter. 

            // If none exists, we don't have an encoding
            int semiColonIndex = contentType.IndexOf(';');
            if (semiColonIndex == -1)
            {
                return null;
            }

            // optimize for charset being the first parameter
            int charsetValueIndex = -1;

            // for Indigo scenarios, we'll have "; charset=", so check for the c
            if ((contentType.Length > semiColonIndex + 11) // need room for parameter + charset + '=' 
                && contentType[semiColonIndex + 2] == 'c'
                && string.Compare("charset=", 0, contentType, semiColonIndex + 2, 8, StringComparison.OrdinalIgnoreCase) == 0)
            {
                charsetValueIndex = semiColonIndex + 10;
            }
            else
            {
                // look for charset= somewhere else in the message
                int paramIndex = contentType.IndexOf("charset=", semiColonIndex + 1, StringComparison.OrdinalIgnoreCase);
                if (paramIndex != -1)
                {
                    // validate there's only whitespace or semi-colons beforehand
                    for (int i = paramIndex - 1; i >= semiColonIndex; i--)
                    {
                        if (contentType[i] == ';')
                        {
                            charsetValueIndex = paramIndex + 8;
                            break;
                        }

                        if (contentType[i] == '\n')
                        {
                            if (i == semiColonIndex || contentType[i - 1] != '\r')
                            {
                                break;
                            }

                            i--;
                            continue;
                        }

                        if (contentType[i] != ' '
                            && contentType[i] != '\t')
                        {
                            break;
                        }
                    }
                }
            }

            string charSet;
            Encoding enc;

            // we have a possible charset value. If it's easy to parse, do so
            if (charsetValueIndex != -1)
            {
                // get the next semicolon
                semiColonIndex = contentType.IndexOf(';', charsetValueIndex);
                if (semiColonIndex == -1)
                {
                    charSet = contentType.Substring(charsetValueIndex);
                }
                else
                {
                    charSet = contentType.Substring(charsetValueIndex, semiColonIndex - charsetValueIndex);
                }

                // and some minimal quote stripping
                if (charSet.Length > 2 && charSet[0] == '"' && charSet[charSet.Length - 1] == '"')
                {
                    charSet = charSet.Substring(1, charSet.Length - 2);
                }

                if (TryGetEncodingFromCharSet(charSet, out enc))
                {
                    return enc;
                }
            }

            // our quick heuristics failed. fall back to System.Net
            try
            {
                MediaTypeHeaderValue parsedContentType = MediaTypeHeaderValue.Parse(contentType);
                charSet = parsedContentType.CharSet;
            }
            catch (FormatException e)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ProtocolException(SR.EncoderBadContentType, e));
            }

            if (TryGetEncodingFromCharSet(charSet, out enc))
            {
                return enc;
            }

            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ProtocolException(SR.Format(SR.EncoderUnrecognizedCharSet, charSet)));
        }

        internal static bool TryGetEncodingFromCharSet(string charSet, out Encoding encoding)
        {
            encoding = null;
            if (charSet == null || charSet.Length == 0)
            {
                return true;
            }

            return TextEncoderDefaults.TryGetEncoding(charSet, out encoding);
        }

        private class TextMessageEncoder : MessageEncoder
        {

            // Double-checked locking pattern requires volatile for read/write synchronization
            private volatile SynchronizedPool<XmlDictionaryWriter> _streamedWriterPool;
            private volatile SynchronizedPool<XmlDictionaryReader> _streamedReaderPool;
            private volatile SynchronizedPool<UTF8BufferedMessageData> _bufferedReaderPool;
            private volatile SynchronizedPool<TextBufferedMessageWriter> _bufferedWriterPool;
            private volatile SynchronizedPool<RecycledMessageState> _recycledStatePool;
            private readonly string _contentType;
            private readonly string _mediaType;
            private readonly Encoding _writeEncoding;
            private readonly MessageVersion _version;
            private readonly bool _optimizeWriteForUTF8;
            private const int maxPooledXmlReadersPerMessage = 2;
            private readonly XmlDictionaryReaderQuotas _bufferedReadReaderQuotas;
            private readonly OnXmlDictionaryReaderClose _onStreamedReaderClose;
            private readonly ContentEncoding[] _contentEncodingMap;

            public TextMessageEncoder(MessageVersion version, Encoding writeEncoding, int maxReadPoolSize, int maxWritePoolSize, XmlDictionaryReaderQuotas quotas)
            {
                if (writeEncoding == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(writeEncoding));
                }

                TextEncoderDefaults.ValidateEncoding(writeEncoding);
                _writeEncoding = writeEncoding;
                _optimizeWriteForUTF8 = IsUTF8Encoding(writeEncoding);

                ThisLock = new object();

                _version = version ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(version));
                MaxReadPoolSize = maxReadPoolSize;
                MaxWritePoolSize = maxWritePoolSize;

                ReaderQuotas = new XmlDictionaryReaderQuotas();
                quotas.CopyTo(ReaderQuotas);

                _bufferedReadReaderQuotas = EncoderHelpers.GetBufferedReadQuotas(ReaderQuotas);

                _onStreamedReaderClose = new OnXmlDictionaryReaderClose(ReturnStreamedReader);

                _mediaType = GetMediaType(version);
                _contentType = GetContentType(_mediaType, writeEncoding);
                if (version.Envelope == EnvelopeVersion.Soap12)
                {
                    _contentEncodingMap = Soap12Content;
                }
                else if (version.Envelope == EnvelopeVersion.Soap11)
                {
                    _contentEncodingMap = Soap11Content;
                }
                else if (version.Envelope == EnvelopeVersion.None)
                {
                    _contentEncodingMap = SoapNoneContent;
                }
                else
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                        SR.Format(SR.EnvelopeVersionNotSupported, version.Envelope)));
                }
            }

            private static bool IsUTF8Encoding(Encoding encoding)
            {
                return encoding.WebName == "utf-8";
            }

            public override string ContentType
            {
                get { return _contentType; }
            }

            public int MaxWritePoolSize { get; }

            public int MaxReadPoolSize { get; }

            public XmlDictionaryReaderQuotas ReaderQuotas { get; }

            public override string MediaType
            {
                get { return _mediaType; }
            }

            public override MessageVersion MessageVersion
            {
                get { return _version; }
            }

            private object ThisLock { get; }


            protected override bool IsCharSetSupported(string charSet)
            {
                Encoding tmp;
                if (!TextEncoderDefaults.TryGetEncoding(charSet, out tmp))
                {
                    // GetEncodingFromContentType supports charset with quotes (by simply stripping them) so we do the same here
                    // This also gives us parity with Desktop WCF behavior
                    if (charSet.Length > 2 && charSet[0] == '"' && charSet[charSet.Length - 1] == '"')
                    {
                        charSet = charSet.Substring(1, charSet.Length - 2);
                        return TextEncoderDefaults.TryGetEncoding(charSet, out tmp);
                    }
                    return false;
                }

                return true;
            }

            public override bool IsContentTypeSupported(string contentType)
            {
                if (contentType == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(contentType));
                }

                if (base.IsContentTypeSupported(contentType))
                {
                    return true;
                }

                // we support a few extra content types for "none"
                if (MessageVersion == MessageVersion.None)
                {
                    const string rss1MediaType = "text/xml";
                    const string rss2MediaType = "application/rss+xml";
                    const string atomMediaType = "application/atom+xml";
                    const string htmlMediaType = "text/html";

                    if (IsContentTypeSupported(contentType, rss1MediaType, rss1MediaType))
                    {
                        return true;
                    }
                    if (IsContentTypeSupported(contentType, rss2MediaType, rss2MediaType))
                    {
                        return true;
                    }
                    if (IsContentTypeSupported(contentType, htmlMediaType, htmlMediaType))
                    {
                        return true;
                    }
                    if (IsContentTypeSupported(contentType, atomMediaType, atomMediaType))
                    {
                        return true;
                    }
                    // application/xml checked by base method
                }

                return false;
            }

            public override Message ReadMessage(ArraySegment<byte> buffer, BufferManager bufferManager, string contentType)
            {
                if (bufferManager == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(bufferManager));
                }

                Message message;

                UTF8BufferedMessageData messageData = TakeBufferedReader();
                messageData.Encoding = GetEncodingFromContentType(contentType, _contentEncodingMap);
                messageData.Open(buffer, bufferManager);
                RecycledMessageState messageState = messageData.TakeMessageState();
                if (messageState == null)
                {
                    messageState = new RecycledMessageState();
                }

                message = new BufferedMessage(messageData, messageState);

                message.Properties.Encoder = this;

                return message;
            }

            public override Task<Message> ReadMessageAsync(Stream stream, int maxSizeOfHeaders, string contentType)
            {
                if (stream == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(stream)));
                }

                XmlReader reader = TakeStreamedReader(stream, GetEncodingFromContentType(contentType, _contentEncodingMap));
                Message message = Message.CreateMessage(reader, maxSizeOfHeaders, _version);
                message.Properties.Encoder = this;

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
                                                                SRCommon.ValueMustBeNonNegative), message);
                }

                if (messageOffset < 0 || messageOffset > maxMessageSize)
                {
                    throw TraceUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(messageOffset), messageOffset,
                                                    SR.Format(SR.ValueMustBeInRange, 0, maxMessageSize)), message);
                }

                ThrowIfMismatchedMessageVersion(message);

                message.Properties.Encoder = this;
                TextBufferedMessageWriter messageWriter = TakeBufferedWriter();
                ArraySegment<byte> messageData = messageWriter.WriteMessage(message, bufferManager, messageOffset, maxMessageSize);
                ReturnMessageWriter(messageWriter);

                return messageData;
            }

            public override async Task WriteMessageAsync(Message message, Stream stream)
            {
                if (message == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(message));
                }

                if (stream == null)
                {
                    throw TraceUtility.ThrowHelperError(new ArgumentNullException(nameof(stream)), message);
                }

                ThrowIfMismatchedMessageVersion(message);

                message.Properties.Encoder = this;
                XmlDictionaryWriter xmlWriter = TakeStreamedWriter(stream);
                if (_optimizeWriteForUTF8)
                {
                    await message.WriteMessageAsync(xmlWriter);
                }
                else
                {
                    xmlWriter.WriteStartDocument();
                    await message.WriteMessageAsync(xmlWriter);
                    xmlWriter.WriteEndDocument();
                }

                xmlWriter.Flush();
                ReturnStreamedWriter(xmlWriter);
                await stream.FlushAsync();
            }

            private XmlDictionaryWriter TakeStreamedWriter(Stream stream)
            {
                if (_streamedWriterPool == null)
                {
                    lock (ThisLock)
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
                    xmlWriter = XmlDictionaryWriter.CreateTextWriter(stream, _writeEncoding, false);
                }
                // TODO: Use the reinitialization API's once moved to .Net Standard 2.0
                //else
                //{
                //    ((IXmlTextWriterInitializer)xmlWriter).SetOutput(stream, this.writeEncoding, false);
                //}
                return xmlWriter;
            }

            private void ReturnStreamedWriter(XmlWriter xmlWriter)
            {
                xmlWriter.Dispose();
                // TODO: Use the reinitialization API's once moved to .Net Standard 2.0
                //streamedWriterPool.Return((XmlDictionaryWriter)xmlWriter);
            }

            private TextBufferedMessageWriter TakeBufferedWriter()
            {
                if (_bufferedWriterPool == null)
                {
                    lock (ThisLock)
                    {
                        if (_bufferedWriterPool == null)
                        {
                            _bufferedWriterPool = new SynchronizedPool<TextBufferedMessageWriter>(MaxWritePoolSize);
                        }
                    }
                }

                TextBufferedMessageWriter messageWriter = _bufferedWriterPool.Take();
                if (messageWriter == null)
                {
                    messageWriter = new TextBufferedMessageWriter(this);
                }
                return messageWriter;
            }

            private void ReturnMessageWriter(TextBufferedMessageWriter messageWriter)
            {
                _bufferedWriterPool.Return(messageWriter);
            }

            private XmlReader TakeStreamedReader(Stream stream, Encoding enc)
            {
                if (_streamedReaderPool == null)
                {
                    lock (ThisLock)
                    {
                        if (_streamedReaderPool == null)
                        {
                            _streamedReaderPool = new SynchronizedPool<XmlDictionaryReader>(MaxReadPoolSize);
                        }
                    }
                }
                XmlDictionaryReader xmlReader = _streamedReaderPool.Take();
                if (xmlReader == null)
                {
                    xmlReader = XmlDictionaryReader.CreateTextReader(stream, enc, ReaderQuotas, null);
                }
                else
                {
                    ((IXmlTextReaderInitializer)xmlReader).SetInput(stream, enc, ReaderQuotas, _onStreamedReaderClose);
                }
                return xmlReader;
            }

            private void ReturnStreamedReader(XmlDictionaryReader xmlReader)
            {
                _streamedReaderPool.Return(xmlReader);
            }

            private XmlDictionaryWriter CreateWriter(Stream stream)
            {
                return XmlDictionaryWriter.CreateTextWriter(stream, _writeEncoding, false);
            }

            private UTF8BufferedMessageData TakeBufferedReader()
            {
                if (_bufferedReaderPool == null)
                {
                    lock (ThisLock)
                    {
                        if (_bufferedReaderPool == null)
                        {
                            _bufferedReaderPool = new SynchronizedPool<UTF8BufferedMessageData>(MaxReadPoolSize);
                        }
                    }
                }
                UTF8BufferedMessageData messageData = _bufferedReaderPool.Take();
                if (messageData == null)
                {
                    messageData = new UTF8BufferedMessageData(this, maxPooledXmlReadersPerMessage);
                }
                return messageData;
            }

            private void ReturnBufferedData(UTF8BufferedMessageData messageData)
            {
                _bufferedReaderPool.Return(messageData);
            }

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
                                _recycledStatePool = new SynchronizedPool<RecycledMessageState>(MaxReadPoolSize);
                            }
                        }
                    }
                    return _recycledStatePool;
                }
            }

            private class UTF8BufferedMessageData : BufferedMessageData
            {
                private readonly TextMessageEncoder _messageEncoder;
                private readonly Pool<XmlDictionaryReader> _readerPool;
                private readonly OnXmlDictionaryReaderClose _onClose;
                private Encoding _encoding;
                private const int additionalNodeSpace = 1024;

                public UTF8BufferedMessageData(TextMessageEncoder messageEncoder, int maxReaderPoolSize)
                    : base(messageEncoder.RecycledStatePool)
                {
                    _messageEncoder = messageEncoder;
                    _readerPool = new Pool<XmlDictionaryReader>(maxReaderPoolSize);
                    _onClose = new OnXmlDictionaryReaderClose(OnXmlReaderClosed);
                }

                internal Encoding Encoding
                {
                    set
                    {
                        _encoding = value;
                    }
                }

                public override MessageEncoder MessageEncoder
                {
                    get { return _messageEncoder; }
                }

                public override XmlDictionaryReaderQuotas Quotas
                {
                    get { return _messageEncoder._bufferedReadReaderQuotas; }
                }

                protected override void OnClosed()
                {
                    _messageEncoder.ReturnBufferedData(this);
                }

                protected override XmlDictionaryReader TakeXmlReader()
                {
                    ArraySegment<byte> buffer = Buffer;

                    XmlDictionaryReader xmlReader = _readerPool.Take();
                    if (xmlReader == null)
                    {
                        // TODO: Use the reinitialization API's once moved to .Net Standard 2.0
                        xmlReader = XmlDictionaryReader.CreateTextReader(buffer.Array, buffer.Offset, buffer.Count, _encoding, Quotas, _onClose);
                    }
                    else
                    {
                        ((IXmlTextReaderInitializer)xmlReader).SetInput(buffer.Array, buffer.Offset, buffer.Count, _encoding, Quotas, _onClose);
                    }

                    return xmlReader;
                }

                protected override void ReturnXmlReader(XmlDictionaryReader xmlReader)
                {
                    if (xmlReader != null)
                    {
                        _readerPool.Return(xmlReader);
                    }
                }
            }

            private class TextBufferedMessageWriter : BufferedMessageWriter
            {
                private readonly TextMessageEncoder _messageEncoder;
                //XmlDictionaryWriter writer;

                public TextBufferedMessageWriter(TextMessageEncoder messageEncoder)
                {
                    _messageEncoder = messageEncoder;
                }

                protected override void OnWriteStartMessage(XmlDictionaryWriter writer)
                {
                    if (!_messageEncoder._optimizeWriteForUTF8)
                    {
                        writer.WriteStartDocument();
                    }
                }

                protected override void OnWriteEndMessage(XmlDictionaryWriter writer)
                {
                    if (!_messageEncoder._optimizeWriteForUTF8)
                    {
                        writer.WriteEndDocument();
                    }
                }

                protected override XmlDictionaryWriter TakeXmlWriter(Stream stream)
                {
                    if (_messageEncoder._optimizeWriteForUTF8)
                    {
                        //XmlDictionaryWriter returnedWriter = writer;
                        //if (returnedWriter == null)
                        //{
                        //    returnedWriter = XmlDictionaryWriter.CreateTextWriter(stream, messageEncoder.writeEncoding, false);
                        //}
                        //else
                        //{
                        //    writer = null;
                        //    ((IXmlTextWriterInitializer)returnedWriter).SetOutput(stream, messageEncoder.writeEncoding, false);
                        //}
                        //return returnedWriter;
                        // TODO: Use IXmlTextWriterInitializer when moved to .Net Standard 2.0
                        return XmlDictionaryWriter.CreateTextWriter(stream, _messageEncoder._writeEncoding, false);
                    }
                    else
                    {
                        return _messageEncoder.CreateWriter(stream);
                    }
                }

                protected override void ReturnXmlWriter(XmlDictionaryWriter writer)
                {
                    Contract.Assert(writer != null, "writer MUST NOT be null");
                    writer.Flush();
                    writer.Dispose();

                    // TODO: Use IXmlTextWriterInitializer reuse once moved to .Net Standard 2.0
                    //if (messageEncoder.optimizeWriteForUTF8)
                    //{
                    //    if (this.writer == null)
                    //        this.writer = writer;
                    //}
                }
            }
        }
    }

    internal class ContentEncoding
    {
        public string contentType;
        public Encoding encoding;
    }
}
