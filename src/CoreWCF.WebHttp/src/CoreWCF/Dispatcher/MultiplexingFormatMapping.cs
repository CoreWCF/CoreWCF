// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Net.Mime;
using System.Text;
using CoreWCF.Channels;
using CoreWCF.Web;

namespace CoreWCF.Dispatcher
{
    internal abstract class MultiplexingFormatMapping
    {
        protected Encoding writeEncoding;
        protected string writeCharset;
        protected WebContentTypeMapper contentTypeMapper;

        public abstract WebMessageFormat MessageFormat { get; }
        public abstract WebContentFormat ContentFormat { get; }
        public abstract string DefaultMediaType { get; }
        protected abstract MessageEncoder Encoder { get; }

        private ContentType _defaultContentType;

        public ContentType DefaultContentType
        {
            get
            {
                if (_defaultContentType == null)
                {
                    _defaultContentType = new ContentType(DefaultMediaType) { CharSet = writeCharset };
                }

                return _defaultContentType;
            }
        }

        public MultiplexingFormatMapping(Encoding writeEncoding, WebContentTypeMapper contentTypeMapper)
        {
            if (writeEncoding == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(writeEncoding));
            }

            this.writeEncoding = writeEncoding;
            writeCharset = TextEncoderDefaults.EncodingToCharSet(writeEncoding);
            this.contentTypeMapper = contentTypeMapper;
        }

        public bool CanFormatResponse(ContentType acceptHeaderElement, bool matchCharset, out ContentType contentType)
        {
            if (acceptHeaderElement == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(acceptHeaderElement));
            }

            // Scrub the content type so that it is only mediaType and the charset
            string charset = acceptHeaderElement.CharSet;
            contentType = new ContentType(acceptHeaderElement.MediaType);
            contentType.CharSet = DefaultContentType.CharSet;
            string contentTypeStr = contentType.ToString();

            if (matchCharset &&
                !string.IsNullOrEmpty(charset) &&
                !string.Equals(charset, DefaultContentType.CharSet, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (contentTypeMapper != null &&
                contentTypeMapper.GetMessageFormatForContentType(contentType.MediaType) == ContentFormat)
            {
                return true;
            }

            if (Encoder.IsContentTypeSupported(contentTypeStr) &&
                (charset == null || contentType.CharSet == DefaultContentType.CharSet))
            {
                return true;
            }

            contentType = null;
            return false;
        }
    }
}
