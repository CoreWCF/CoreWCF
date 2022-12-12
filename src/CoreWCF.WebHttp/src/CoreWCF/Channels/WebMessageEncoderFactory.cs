// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using CoreWCF.Runtime;

namespace CoreWCF.Channels
{
    public class WebMessageEncoderFactory : MessageEncoderFactory
    {
        private readonly WebMessageEncoder _messageEncoder;

        public WebMessageEncoderFactory(Encoding writeEncoding, int maxReadPoolSize, int maxWritePoolSize, XmlDictionaryReaderQuotas quotas, WebContentTypeMapper contentTypeMapper, bool javascriptCallbackEnabled)
        {
            _messageEncoder = new WebMessageEncoder(writeEncoding, maxReadPoolSize, maxWritePoolSize, quotas, contentTypeMapper, javascriptCallbackEnabled);
        }

        public override MessageEncoder Encoder => _messageEncoder;

        public override MessageVersion MessageVersion => _messageEncoder.MessageVersion;

        internal static string GetContentType(string mediaType, Encoding encoding)
        {
            string charset = TextEncoderDefaults.EncodingToCharSet(encoding);
            if (!string.IsNullOrEmpty(charset))
            {
                return string.Format(CultureInfo.InvariantCulture, "{0}; charset={1}", mediaType, charset);
            }

            return mediaType;
        }

        internal class WebMessageEncoder : MessageEncoder
        {
            private const string DefaultMediaType = "application/xml";
            private readonly WebContentTypeMapper _contentTypeMapper;
            private readonly string _defaultContentType;

            // Double-checked locking pattern requires volatile for read/write synchronization
            private volatile MessageEncoder _jsonMessageEncoder;
            private readonly int _maxReadPoolSize;
            private readonly int _maxWritePoolSize;

            // Double-checked locking pattern requires volatile for read/write synchronization
            private volatile MessageEncoder _rawMessageEncoder;
            private readonly XmlDictionaryReaderQuotas _readerQuotas;

            // Double-checked locking pattern requires volatile for read/write synchronization
            private volatile MessageEncoder _textMessageEncoder;
            private readonly Encoding _writeEncoding;
            //bool javascriptCallbackEnabled;

            public WebMessageEncoder(Encoding writeEncoding, int maxReadPoolSize, int maxWritePoolSize, XmlDictionaryReaderQuotas quotas, WebContentTypeMapper contentTypeMapper, bool javascriptCallbackEnabled)
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
                _contentTypeMapper = contentTypeMapper;
                //this.javascriptCallbackEnabled = javascriptCallbackEnabled;

                _readerQuotas = new XmlDictionaryReaderQuotas();
                quotas.CopyTo(_readerQuotas);

                _defaultContentType = GetContentType(DefaultMediaType, writeEncoding);
            }

            public override string ContentType => _defaultContentType;

            public override string MediaType => DefaultMediaType;

            public override MessageVersion MessageVersion => MessageVersion.None;

            private MessageEncoder JsonMessageEncoder
            {
                get
                {
                    if (_jsonMessageEncoder == null)
                    {
                        lock (ThisLock)
                        {
                            if (_jsonMessageEncoder == null)
                            {
                                _jsonMessageEncoder = new JsonMessageEncoderFactory(_writeEncoding, _maxReadPoolSize, _maxWritePoolSize, _readerQuotas, false).Encoder;
                            }
                        }
                    }

                    return _jsonMessageEncoder;
                }
            }

            private MessageEncoder RawMessageEncoder
            {
                get
                {
                    if (_rawMessageEncoder == null)
                    {
                        lock (ThisLock)
                        {
                            if (_rawMessageEncoder == null)
                            {
                                _rawMessageEncoder = new ByteStreamMessageEncodingBindingElement(_readerQuotas).CreateMessageEncoderFactory().Encoder;
                            }
                        }
                    }

                    return _rawMessageEncoder;
                }
            }

            private MessageEncoder TextMessageEncoder
            {
                get
                {
                    if (_textMessageEncoder == null)
                    {
                        lock (ThisLock)
                        {
                            if (_textMessageEncoder == null)
                            {
                                _textMessageEncoder = new TextMessageEncodingBindingElement(MessageVersion.None, _writeEncoding).CreateMessageEncoderFactory().Encoder;
                            }
                        }
                    }

                    return _textMessageEncoder;
                }
            }

            private object ThisLock { get; }

            public override bool IsContentTypeSupported(string contentType)
            {
                if (contentType == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(contentType));
                }

                if (TryGetContentTypeMapping(contentType, out WebContentFormat messageFormat) &&
                    (messageFormat != WebContentFormat.Default))
                {
                    return true;
                }

                return RawMessageEncoder.IsContentTypeSupported(contentType) || JsonMessageEncoder.IsContentTypeSupported(contentType) || TextMessageEncoder.IsContentTypeSupported(contentType);
            }

            public override Message ReadMessage(ArraySegment<byte> buffer, BufferManager bufferManager, string contentType)
            {
                if (bufferManager == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(bufferManager)));
                }

                WebContentFormat format = GetFormatForContentType(contentType);
                Message message;

                switch (format)
                {
                    case WebContentFormat.Json:
                        message = JsonMessageEncoder.ReadMessage(buffer, bufferManager, contentType);
                        message.Properties.Add(WebBodyFormatMessageProperty.Name, WebBodyFormatMessageProperty.JsonProperty);
                        break;
                    case WebContentFormat.Xml:
                        message = TextMessageEncoder.ReadMessage(buffer, bufferManager, contentType);
                        message.Properties.Add(WebBodyFormatMessageProperty.Name, WebBodyFormatMessageProperty.XmlProperty);
                        break;
                    case WebContentFormat.Raw:
                        message = RawMessageEncoder.ReadMessage(buffer, bufferManager, contentType);
                        message.Properties.Add(WebBodyFormatMessageProperty.Name, WebBodyFormatMessageProperty.RawProperty);
                        break;
                    default:
                        throw Fx.AssertAndThrow("This should never get hit because GetFormatForContentType shouldn't return a WebContentFormat other than Json, Xml, and Raw");
                }

                return message;
            }

            public override async Task<Message> ReadMessageAsync(Stream stream, int maxSizeOfHeaders, string contentType)
            {
                if (stream == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(stream)));
                }

                WebContentFormat format = GetFormatForContentType(contentType);
                Message message;
                switch (format)
                {
                    case WebContentFormat.Json:
                        message = await JsonMessageEncoder.ReadMessageAsync(stream, maxSizeOfHeaders, contentType);
                        message.Properties.Add(WebBodyFormatMessageProperty.Name, WebBodyFormatMessageProperty.JsonProperty);
                        break;
                    case WebContentFormat.Xml:
                        message = await TextMessageEncoder.ReadMessageAsync(stream, maxSizeOfHeaders, contentType);
                        message.Properties.Add(WebBodyFormatMessageProperty.Name, WebBodyFormatMessageProperty.XmlProperty);
                        break;
                    case WebContentFormat.Raw:
                        message = await RawMessageEncoder.ReadMessageAsync(stream, maxSizeOfHeaders, contentType);
                        message.Properties.Add(WebBodyFormatMessageProperty.Name, WebBodyFormatMessageProperty.RawProperty);
                        break;
                    default:
                        throw Fx.AssertAndThrow("This should never get hit because GetFormatForContentType shouldn't return a WebContentFormat other than Json, Xml, and Raw");
                }
                return message;
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

                ThrowIfMismatchedMessageVersion(message);

                WebContentFormat messageFormat = ExtractFormatFromMessage(message);
                //JavascriptCallbackResponseMessageProperty javascriptResponseMessageProperty;
                switch (messageFormat)
                {
                    case WebContentFormat.Json:
                        return JsonMessageEncoder.WriteMessage(message, maxMessageSize, bufferManager, messageOffset);
                    case WebContentFormat.Xml:
                        //if (message.Properties.TryGetValue<JavascriptCallbackResponseMessageProperty>(JavascriptCallbackResponseMessageProperty.Name, out javascriptResponseMessageProperty) &&
                        //    javascriptResponseMessageProperty != null &&
                        //    !String.IsNullOrEmpty(javascriptResponseMessageProperty.CallbackFunctionName))
                        //{
                        //    throw TraceUtility.ThrowHelperError(new InvalidOperationException(SR.JavascriptCallbackNotsupported), message);
                        //}
                        return TextMessageEncoder.WriteMessage(message, maxMessageSize, bufferManager, messageOffset);
                    case WebContentFormat.Raw:
                        //if (message.Properties.TryGetValue<JavascriptCallbackResponseMessageProperty>(JavascriptCallbackResponseMessageProperty.Name, out javascriptResponseMessageProperty) &&
                        //    javascriptResponseMessageProperty != null &&
                        //    !String.IsNullOrEmpty(javascriptResponseMessageProperty.CallbackFunctionName))
                        //{
                        //    throw TraceUtility.ThrowHelperError(new InvalidOperationException(SR.JavascriptCallbackNotsupported), message);
                        //}
                        return RawMessageEncoder.WriteMessage(message, maxMessageSize, bufferManager, messageOffset);
                    default:
                        throw Fx.AssertAndThrow("This should never get hit because GetFormatForContentType shouldn't return a WebContentFormat other than Json, Xml, and Raw");
                }
            }

            public override async Task WriteMessageAsync(Message message, Stream stream)
            {
                if (message == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(message)));
                }

                if (stream == null)
                {
                    throw TraceUtility.ThrowHelperError(new ArgumentNullException("stream"), message);
                }

                ThrowIfMismatchedMessageVersion(message);

                WebContentFormat messageFormat = ExtractFormatFromMessage(message);
                //JavascriptCallbackResponseMessageProperty javascriptResponseMessageProperty;
                switch (messageFormat)
                {
                    case WebContentFormat.Json:
                        await JsonMessageEncoder.WriteMessageAsync(message, stream);
                        break;
                    case WebContentFormat.Xml:
                        //if (message.Properties.TryGetValue<JavascriptCallbackResponseMessageProperty>(JavascriptCallbackResponseMessageProperty.Name, out javascriptResponseMessageProperty) &&
                        //    javascriptResponseMessageProperty != null &&
                        //    !string.IsNullOrEmpty(javascriptResponseMessageProperty.CallbackFunctionName))
                        //{
                        //    throw TraceUtility.ThrowHelperError(new InvalidOperationException(SR.JavascriptCallbackNotsupported), message);
                        //}
                        await TextMessageEncoder.WriteMessageAsync(message, stream);
                        break;
                    case WebContentFormat.Raw:
                        //if (message.Properties.TryGetValue<JavascriptCallbackResponseMessageProperty>(JavascriptCallbackResponseMessageProperty.Name, out javascriptResponseMessageProperty) &&
                        //    javascriptResponseMessageProperty != null &&
                        //    !string.IsNullOrEmpty(javascriptResponseMessageProperty.CallbackFunctionName))
                        //{
                        //    throw TraceUtility.ThrowHelperError(new InvalidOperationException(SR.JavascriptCallbackNotsupported), message);
                        //}
                        await RawMessageEncoder.WriteMessageAsync(message, stream);
                        break;
                    default:
                        throw Fx.AssertAndThrow("This should never get hit because GetFormatForContentType shouldn't return a WebContentFormat other than Json, Xml, and Raw");
                }
            }

            protected override bool IsCharSetSupported(string charSet) => TextEncoderDefaults.TryGetEncoding(charSet, out _);

            private WebContentFormat ExtractFormatFromMessage(Message message)
            {
                message.Properties.TryGetValue(WebBodyFormatMessageProperty.Name, out object messageFormatProperty);
                if (messageFormatProperty == null)
                {
                    return WebContentFormat.Xml;
                }

                if ((!(messageFormatProperty is WebBodyFormatMessageProperty typedMessageFormatProperty)) ||
                    (typedMessageFormatProperty.Format == WebContentFormat.Default))
                {
                    return WebContentFormat.Xml;
                }

                return typedMessageFormatProperty.Format;
            }

            private WebContentFormat GetFormatForContentType(string contentType)
            {
                if (TryGetContentTypeMapping(contentType, out WebContentFormat messageFormat) &&
                    (messageFormat != WebContentFormat.Default))
                {
                    //if (DiagnosticUtility.ShouldTraceInformation)
                    //{
                    //    if (string.IsNullOrEmpty(contentType))
                    //    {
                    //        contentType = "<null>";
                    //    }
                    //    TraceUtility.TraceEvent(TraceEventType.Information,
                    //        TraceCode.RequestFormatSelectedFromContentTypeMapper,
                    //        SR.GetString(SR.TraceCodeRequestFormatSelectedFromContentTypeMapper, messageFormat.ToString(), contentType));
                    //}
                    return messageFormat;
                }

                // Don't pass on null content types to IsContentTypeSupported methods -- they might throw.
                // If null content type isn't already mapped, return the default format of Raw.

                if (contentType == null)
                {
                    messageFormat = WebContentFormat.Raw;
                }
                else if (JsonMessageEncoder.IsContentTypeSupported(contentType))
                {
                    messageFormat = WebContentFormat.Json;
                }
                else if (TextMessageEncoder.IsContentTypeSupported(contentType))
                {
                    messageFormat = WebContentFormat.Xml;
                }
                else
                {
                    messageFormat = WebContentFormat.Raw;
                }

                //if (DiagnosticUtility.ShouldTraceInformation)
                //{
                //    TraceUtility.TraceEvent(TraceEventType.Information,
                //        TraceCode.RequestFormatSelectedByEncoderDefaults,
                //        SR.GetString(SR.TraceCodeRequestFormatSelectedByEncoderDefaults, messageFormat.ToString(), contentType));
                //}

                return messageFormat;
            }

           private bool TryGetContentTypeMapping(string contentType, out WebContentFormat format)
            {
                if (_contentTypeMapper == null)
                {
                    format = WebContentFormat.Default;
                    return false;
                }

                try
                {
                    format = _contentTypeMapper.GetMessageFormatForContentType(contentType);
                    if (!WebContentFormatHelper.IsDefined(format))
                    {
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.Format(SR.UnknownWebEncodingFormat, contentType, format)));
                    }
                    return true;
                }
                catch (Exception e)
                {
                    if (Fx.IsFatal(e))
                    {
                        throw;
                    }

                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new CommunicationException(
                        SR.Format(SR.ErrorEncounteredInContentTypeMapper), e));
                }
            }
        }
    }
}
