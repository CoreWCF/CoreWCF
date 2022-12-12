// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security;
using System.Security.Permissions;
using System.Text;
using System.Xml;

using CoreWCF.Runtime;
using CoreWCF.Text;

namespace CoreWCF.Xml
{
    internal interface IXmlMtomReaderInitializer
    {
        void SetInput(byte[] buffer, int offset, int count, Encoding[] encodings, string contentType, XmlDictionaryReaderQuotas quotas, int maxBufferSize, OnXmlDictionaryReaderClose onClose);
        void SetInput(Stream stream, Encoding[] encodings, string contentType, XmlDictionaryReaderQuotas quotas, int maxBufferSize, OnXmlDictionaryReaderClose onClose);
    }

    internal class XmlMtomReader : XmlDictionaryReader, IXmlLineInfo, IXmlMtomReaderInitializer
    {
        public static XmlDictionaryReader Create(Stream stream, Encoding encoding, XmlDictionaryReaderQuotas quotas)
        {
            if (encoding == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(encoding));

            return Create(stream, new Encoding[1] { encoding }, quotas);
        }

        public static XmlDictionaryReader Create(Stream stream, Encoding[] encodings, XmlDictionaryReaderQuotas quotas) => Create(stream, encodings, null, quotas);

        public static XmlDictionaryReader Create(Stream stream, Encoding[] encodings, string contentType, XmlDictionaryReaderQuotas quotas) => Create(stream, encodings, contentType, quotas, int.MaxValue, null);

        public static XmlDictionaryReader Create(Stream stream, Encoding[] encodings, string contentType,
            XmlDictionaryReaderQuotas quotas, int maxBufferSize, OnXmlDictionaryReaderClose onClose)
        {
            XmlMtomReader reader = new XmlMtomReader();
            reader.SetInput(stream, encodings, contentType, quotas, maxBufferSize, onClose);
            return reader;
        }

        public static XmlDictionaryReader Create(byte[] buffer, int offset, int count, Encoding encoding, XmlDictionaryReaderQuotas quotas)
        {
            if (encoding == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(encoding));

            return Create(buffer, offset, count, new Encoding[1] { encoding }, quotas);
        }

        public static XmlDictionaryReader Create(byte[] buffer, int offset, int count, Encoding[] encodings, XmlDictionaryReaderQuotas quotas) => Create(buffer, offset, count, encodings, null, quotas);

        public static XmlDictionaryReader Create(byte[] buffer, int offset, int count, Encoding[] encodings, string contentType, XmlDictionaryReaderQuotas quotas) => Create(buffer, offset, count, encodings, contentType, quotas, int.MaxValue, null);

        public static XmlDictionaryReader Create(byte[] buffer, int offset, int count, Encoding[] encodings, string contentType,
            XmlDictionaryReaderQuotas quotas, int maxBufferSize, OnXmlDictionaryReaderClose onClose)
        {
            XmlMtomReader reader = new XmlMtomReader();
            reader.SetInput(buffer, offset, count, encodings, contentType, quotas, maxBufferSize, onClose);
            return reader;
        }

        private Encoding[] _encodings;
        private XmlDictionaryReader _xmlReader;
        private XmlDictionaryReader _infosetReader;
        private MimeReader _mimeReader;
        private Dictionary<string, MimePart> _mimeParts;
        private OnXmlDictionaryReaderClose _onClose;
        private bool _readingBinaryElement;
        private int _maxBufferSize;
        private int _bufferRemaining;
        private MimePart _part;

        public XmlMtomReader()
        {
        }

        internal static void DecrementBufferQuota(int maxBuffer, ref int remaining, int size)
        {
            if (remaining - size <= 0)
            {
                remaining = 0;
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.MtomBufferQuotaExceeded, maxBuffer)));
            }
            else
            {
                remaining -= size;
            }
        }

        private void SetReadEncodings(Encoding[] encodings)
        {
            if (encodings == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(encodings));

            for (int i = 0; i < encodings.Length; i++)
            {
                if (encodings[i] == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(string.Format(CultureInfo.InvariantCulture, "encodings[{0}]", i));
            }

            _encodings = new Encoding[encodings.Length];
            encodings.CopyTo(_encodings, 0);
        }

        private void CheckContentType(string contentType)
        {
            if (contentType != null && contentType.Length == 0)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.MtomContentTypeInvalid, nameof(contentType)));
        }

        public void SetInput(byte[] buffer, int offset, int count, Encoding[] encodings, string contentType, XmlDictionaryReaderQuotas quotas, int maxBufferSize, OnXmlDictionaryReaderClose onClose) => SetInput(new MemoryStream(buffer, offset, count), encodings, contentType, quotas, maxBufferSize, onClose);

        public void SetInput(Stream stream, Encoding[] encodings, string contentType, XmlDictionaryReaderQuotas quotas, int maxBufferSize, OnXmlDictionaryReaderClose onClose)
        {
            SetReadEncodings(encodings);
            CheckContentType(contentType);
            Initialize(stream, contentType, quotas, maxBufferSize);
            _onClose = onClose;
        }

        private void Initialize(Stream stream, string contentType, XmlDictionaryReaderQuotas quotas, int maxBufferSize)
        {
            if (stream == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(stream));

            _maxBufferSize = maxBufferSize;
            _bufferRemaining = maxBufferSize;

            string boundary, start, startInfo;

            if (contentType == null)
            {
                MimeMessageReader messageReader = new MimeMessageReader(stream);
                MimeHeaders messageHeaders = messageReader.ReadHeaders(_maxBufferSize, ref _bufferRemaining);
                ReadMessageMimeVersionHeader(messageHeaders.MimeVersion);
                ReadMessageContentTypeHeader(messageHeaders.ContentType, out boundary, out start, out startInfo);
                stream = messageReader.GetContentStream();
                if (stream == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.MtomMessageInvalidContent));
            }
            else
            {
                ReadMessageContentTypeHeader(new ContentTypeHeader(contentType), out boundary, out start, out startInfo);
            }

            _mimeReader = new MimeReader(stream, boundary);
            _mimeParts = null;
            _readingBinaryElement = false;

            MimePart infosetPart = (start == null) ? ReadRootMimePart() : ReadMimePart(GetStartUri(start));
            byte[] infosetBytes = infosetPart.GetBuffer(_maxBufferSize, ref _bufferRemaining);
            int infosetByteCount = (int)infosetPart.Length;

            Encoding encoding = ReadRootContentTypeHeader(infosetPart.Headers.ContentType, _encodings, startInfo);
            CheckContentTransferEncodingOnRoot(infosetPart.Headers.ContentTransferEncoding);

            IXmlTextReaderInitializer initializer = _xmlReader as IXmlTextReaderInitializer;

            if (initializer != null)
                initializer.SetInput(infosetBytes, 0, infosetByteCount, encoding, quotas, null);
            else
                _xmlReader = XmlDictionaryReader.CreateTextReader(infosetBytes, 0, infosetByteCount, encoding, quotas, null);
        }

        public override XmlDictionaryReaderQuotas Quotas => _xmlReader.Quotas;

        private void ReadMessageMimeVersionHeader(MimeVersionHeader header)
        {
            if (header != null && header.Version != MimeVersionHeader.Default.Version)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.MtomMessageInvalidMimeVersion, header.Version, MimeVersionHeader.Default.Version)));
        }

        private void ReadMessageContentTypeHeader(ContentTypeHeader header, out string boundary, out string start, out string startInfo)
        {
            if (header == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.MtomMessageContentTypeNotFound));

            if (string.Compare(MtomGlobals.MediaType, header.MediaType, StringComparison.OrdinalIgnoreCase) != 0
                || string.Compare(MtomGlobals.MediaSubtype, header.MediaSubtype, StringComparison.OrdinalIgnoreCase) != 0)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.MtomMessageNotMultipart, MtomGlobals.MediaType, MtomGlobals.MediaSubtype)));

            string type;
            if (!header.Parameters.TryGetValue(MtomGlobals.TypeParam, out type) || MtomGlobals.XopType != type)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.MtomMessageNotApplicationXopXml, MtomGlobals.XopType)));

            if (!header.Parameters.TryGetValue(MtomGlobals.BoundaryParam, out boundary))
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.MtomMessageRequiredParamNotSpecified, MtomGlobals.BoundaryParam)));
            if (!MailBnfHelper.IsValidMimeBoundary(boundary))
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.MtomBoundaryInvalid, boundary)));

            if (!header.Parameters.TryGetValue(MtomGlobals.StartParam, out start))
                start = null;

            if (!header.Parameters.TryGetValue(MtomGlobals.StartInfoParam, out startInfo))
                startInfo = null;
        }

        private Encoding ReadRootContentTypeHeader(ContentTypeHeader header, Encoding[] expectedEncodings, string expectedType)
        {
            if (header == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.MtomRootContentTypeNotFound));

            if (string.Compare(MtomGlobals.XopMediaType, header.MediaType, StringComparison.OrdinalIgnoreCase) != 0
                || string.Compare(MtomGlobals.XopMediaSubtype, header.MediaSubtype, StringComparison.OrdinalIgnoreCase) != 0)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.MtomRootNotApplicationXopXml, MtomGlobals.XopMediaType, MtomGlobals.XopMediaSubtype)));

            string charset;
            if (!header.Parameters.TryGetValue(MtomGlobals.CharsetParam, out charset)
                || charset == null || charset.Length == 0)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.MtomRootRequiredParamNotSpecified, MtomGlobals.CharsetParam)));
            Encoding encoding = null;
            for (int i = 0; i < _encodings.Length; i++)
            {
                if (string.Compare(charset, expectedEncodings[i].WebName, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    encoding = expectedEncodings[i];
                    break;
                }
            }
            if (encoding == null)
            {
                // Check for alternate names
                if (string.Compare(charset, "utf-16LE", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    for (int i = 0; i < _encodings.Length; i++)
                    {
                        if (string.Compare(expectedEncodings[i].WebName, Encoding.Unicode.WebName, StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            encoding = expectedEncodings[i];
                            break;
                        }
                    }
                }
                else if (string.Compare(charset, "utf-16BE", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    for (int i = 0; i < _encodings.Length; i++)
                    {
                        if (string.Compare(expectedEncodings[i].WebName, Encoding.BigEndianUnicode.WebName, StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            encoding = expectedEncodings[i];
                            break;
                        }
                    }
                }

                if (encoding == null)
                {
                    StringBuilder expectedCharSetStr = new StringBuilder();
                    for (int i = 0; i < _encodings.Length; i++)
                    {
                        if (expectedCharSetStr.Length != 0)
                            expectedCharSetStr.Append(" | ");
                        expectedCharSetStr.Append(_encodings[i].WebName);
                    }
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.MtomRootUnexpectedCharset, charset, expectedCharSetStr.ToString())));
                }
            }

            if (expectedType != null)
            {
                string rootType;
                if (!header.Parameters.TryGetValue(MtomGlobals.TypeParam, out rootType)
                    || rootType == null || rootType.Length == 0)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.MtomRootRequiredParamNotSpecified, MtomGlobals.TypeParam)));
                if (rootType != expectedType)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.MtomRootUnexpectedType, rootType, expectedType)));
            }

            return encoding;
        }

        // 7bit is default encoding in the absence of content-transfer-encoding header
        private void CheckContentTransferEncodingOnRoot(ContentTransferEncodingHeader header)
        {
            if (header != null && header.ContentTransferEncoding == ContentTransferEncoding.Other)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.MtomContentTransferEncodingNotSupported,
                                                                                      header.Value,
                                                                                      ContentTransferEncodingHeader.SevenBit.ContentTransferEncodingValue,
                                                                                      ContentTransferEncodingHeader.EightBit.ContentTransferEncodingValue,
                                                                                      ContentTransferEncodingHeader.Binary.ContentTransferEncodingValue)));
        }

        private void CheckContentTransferEncodingOnBinaryPart(ContentTransferEncodingHeader header)
        {
            if (header == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.MtomContentTransferEncodingNotPresent,
                    ContentTransferEncodingHeader.Binary.ContentTransferEncodingValue)));
            else if (header.ContentTransferEncoding != ContentTransferEncoding.Binary)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.MtomInvalidTransferEncodingForMimePart,
                    header.Value, ContentTransferEncodingHeader.Binary.ContentTransferEncodingValue)));
        }

        private string GetStartUri(string startUri)
        {
            if (startUri.StartsWith("<", StringComparison.Ordinal))
            {
                if (startUri.EndsWith(">", StringComparison.Ordinal))
                    return startUri;
                else
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.MtomInvalidStartUri, startUri)));
            }
            else
                return string.Format(CultureInfo.InvariantCulture, "<{0}>", startUri);
        }

        public override bool Read()
        {
            bool retVal = _xmlReader.Read();

            if (_xmlReader.NodeType == XmlNodeType.Element)
            {
                XopIncludeReader binaryDataReader = null;
                if (_xmlReader.IsStartElement(MtomGlobals.XopIncludeLocalName, MtomGlobals.XopIncludeNamespace))
                {
                    string uri = null;
                    while (_xmlReader.MoveToNextAttribute())
                    {
                        if (_xmlReader.LocalName == MtomGlobals.XopIncludeHrefLocalName && _xmlReader.NamespaceURI == MtomGlobals.XopIncludeHrefNamespace)
                            uri = _xmlReader.Value;
                        else if (_xmlReader.NamespaceURI == MtomGlobals.XopIncludeNamespace)
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.MtomXopIncludeInvalidXopAttributes, _xmlReader.LocalName, MtomGlobals.XopIncludeNamespace)));
                    }
                    if (uri == null)
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.MtomXopIncludeHrefNotSpecified, MtomGlobals.XopIncludeHrefLocalName)));

                    MimePart mimePart = ReadMimePart(uri);

                    CheckContentTransferEncodingOnBinaryPart(mimePart.Headers.ContentTransferEncoding);

                    _part = mimePart;
                    binaryDataReader = new XopIncludeReader(mimePart, _xmlReader);
                    binaryDataReader.Read();

                    _xmlReader.MoveToElement();
                    if (_xmlReader.IsEmptyElement)
                    {
                        _xmlReader.Read();
                    }
                    else
                    {
                        int xopDepth = _xmlReader.Depth;
                        _xmlReader.ReadStartElement();

                        while (_xmlReader.Depth > xopDepth)
                        {
                            if (_xmlReader.IsStartElement() && _xmlReader.NamespaceURI == MtomGlobals.XopIncludeNamespace)
                                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.MtomXopIncludeInvalidXopElement, _xmlReader.LocalName, MtomGlobals.XopIncludeNamespace)));

                            _xmlReader.Skip();
                        }

                        _xmlReader.ReadEndElement();
                    }
                }

                if (binaryDataReader != null)
                {
                    _xmlReader.MoveToContent();
                    _infosetReader = _xmlReader;
                    _xmlReader = binaryDataReader;
                    binaryDataReader = null;
                }
            }

            if (_xmlReader.ReadState == ReadState.EndOfFile && _infosetReader != null)
            {
                // Read past the containing EndElement if necessary
                if (!retVal)
                    retVal = _infosetReader.Read();

                _part.Release(_maxBufferSize, ref _bufferRemaining);
                _xmlReader = _infosetReader;
                _infosetReader = null;
            }

            return retVal;
        }

        private MimePart ReadMimePart(string uri)
        {
            MimePart part = null;

            if (uri == null || uri.Length == 0)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.MtomInvalidEmptyURI));

            string contentID = null;
            if (uri.StartsWith(MimeGlobals.ContentIDScheme, StringComparison.Ordinal))
                contentID = string.Format(CultureInfo.InvariantCulture, "<{0}>", Uri.UnescapeDataString(uri.Substring(MimeGlobals.ContentIDScheme.Length)));
            else if (uri.StartsWith("<", StringComparison.Ordinal))
                contentID = uri;

            if (contentID == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.MtomInvalidCIDUri, uri)));

            if (_mimeParts != null && _mimeParts.TryGetValue(contentID, out part))
            {
                if (part.ReferencedFromInfoset)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.MtomMimePartReferencedMoreThanOnce, contentID)));
            }
            else
            {
                // TODO: Fix this to be a configurable setting on MtomMessageEncodingBindingElement
                int maxMimeParts = 1000; // AppSettings.MaxMimeParts;
                while (part == null && _mimeReader.ReadNextPart())
                {
                    MimeHeaders headers = _mimeReader.ReadHeaders(_maxBufferSize, ref _bufferRemaining);
                    Stream contentStream = _mimeReader.GetContentStream();
                    if (contentStream == null)
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.MtomMessageInvalidContentInMimePart));

                    ContentIDHeader contentIDHeader = (headers == null) ? null : headers.ContentID;
                    if (contentIDHeader == null || contentIDHeader.Value == null)
                    {
                        // Skip content if Content-ID header is not present
                        int size = 256;
                        byte[] bytes = new byte[size];

                        int read = 0;
                        do
                        {
                            read = contentStream.Read(bytes, 0, size);
                        }
                        while (read > 0);
                        continue;
                    }

                    string currentContentID = headers.ContentID.Value;
                    MimePart currentPart = new MimePart(contentStream, headers);
                    if (_mimeParts == null)
                        _mimeParts = new Dictionary<string, MimePart>();

                    _mimeParts.Add(currentContentID, currentPart);

                    if (_mimeParts.Count > maxMimeParts)
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.MaxMimePartsExceeded, maxMimeParts, "MtomMessageEncoderBindingElement")));

                    if (currentContentID.Equals(contentID))
                        part = currentPart;
                    else
                        currentPart.GetBuffer(_maxBufferSize, ref _bufferRemaining);
                }

                if (part == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.MtomPartNotFound, uri)));
            }

            part.ReferencedFromInfoset = true;
            return part;
        }

        private MimePart ReadRootMimePart()
        {
            MimePart part = null;

            if (!_mimeReader.ReadNextPart())
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.MtomRootPartNotFound));

            MimeHeaders headers = _mimeReader.ReadHeaders(_maxBufferSize, ref _bufferRemaining);
            Stream contentStream = _mimeReader.GetContentStream();
            if (contentStream == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.MtomMessageInvalidContentInMimePart));
            part = new MimePart(contentStream, headers);

            return part;
        }

        private void AdvanceToContentOnElement()
        {
            if (NodeType != XmlNodeType.Attribute)
            {
                MoveToContent();
            }
        }

        public override int AttributeCount => _xmlReader.AttributeCount;

        public override string BaseURI => _xmlReader.BaseURI;

        public override bool CanReadBinaryContent => _xmlReader.CanReadBinaryContent;

        public override bool CanReadValueChunk => _xmlReader.CanReadValueChunk;

        public override bool CanResolveEntity => _xmlReader.CanResolveEntity;

        public override void Close()
        {
            _xmlReader.Close();
            _mimeReader.Close();
            OnXmlDictionaryReaderClose onClose = _onClose;
            _onClose = null;
            if (onClose != null)
            {
                try
                {
                    onClose(this);
                }
                catch (Exception e)
                {
                    if (Fx.IsFatal(e))
                        throw;

                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperCallback(e);
                }
            }
        }

        public override int Depth => _xmlReader.Depth;

        public override bool EOF => _xmlReader.EOF;

        public override string GetAttribute(int index) => _xmlReader.GetAttribute(index);

        public override string GetAttribute(string name) => _xmlReader.GetAttribute(name);

        public override string GetAttribute(string name, string ns) => _xmlReader.GetAttribute(name, ns);

        public override string GetAttribute(XmlDictionaryString localName, XmlDictionaryString ns) => _xmlReader.GetAttribute(localName, ns);

        public override bool HasAttributes => _xmlReader.HasAttributes;

        public override bool HasValue => _xmlReader.HasValue;

        public override bool IsDefault => _xmlReader.IsDefault;

        public override bool IsEmptyElement => _xmlReader.IsEmptyElement;

        public override bool IsLocalName(string localName) => _xmlReader.IsLocalName(localName);

        public override bool IsLocalName(XmlDictionaryString localName) => _xmlReader.IsLocalName(localName);

        public override bool IsNamespaceUri(string ns) => _xmlReader.IsNamespaceUri(ns);

        public override bool IsNamespaceUri(XmlDictionaryString ns) => _xmlReader.IsNamespaceUri(ns);

        public override bool IsStartElement() => _xmlReader.IsStartElement();

        public override bool IsStartElement(string localName) => _xmlReader.IsStartElement(localName);

        public override bool IsStartElement(string localName, string ns) => _xmlReader.IsStartElement(localName, ns);

        public override bool IsStartElement(XmlDictionaryString localName, XmlDictionaryString ns) => _xmlReader.IsStartElement(localName, ns);

        public override string LocalName => _xmlReader.LocalName;

        public override string LookupNamespace(string ns) => _xmlReader.LookupNamespace(ns);

        public override void MoveToAttribute(int index) => _xmlReader.MoveToAttribute(index);

        public override bool MoveToAttribute(string name) => _xmlReader.MoveToAttribute(name);

        public override bool MoveToAttribute(string name, string ns) => _xmlReader.MoveToAttribute(name, ns);

        public override bool MoveToElement() => _xmlReader.MoveToElement();

        public override bool MoveToFirstAttribute() => _xmlReader.MoveToFirstAttribute();

        public override bool MoveToNextAttribute() => _xmlReader.MoveToNextAttribute();

        public override string Name => _xmlReader.Name;

        public override string NamespaceURI => _xmlReader.NamespaceURI;

        public override XmlNameTable NameTable => _xmlReader.NameTable;

        public override XmlNodeType NodeType => _xmlReader.NodeType;

        public override string Prefix => _xmlReader.Prefix;

        public override char QuoteChar => _xmlReader.QuoteChar;

        public override bool ReadAttributeValue() => _xmlReader.ReadAttributeValue();

        public override object ReadContentAs(Type returnType, IXmlNamespaceResolver namespaceResolver)
        {
            AdvanceToContentOnElement();
            return _xmlReader.ReadContentAs(returnType, namespaceResolver);
        }

        public override byte[] ReadContentAsBase64()
        {
            AdvanceToContentOnElement();
            return _xmlReader.ReadContentAsBase64();
        }

        public override int ReadValueAsBase64(byte[] buffer, int offset, int count)
        {
            AdvanceToContentOnElement();
            return _xmlReader.ReadValueAsBase64(buffer, offset, count);
        }

        public override int ReadContentAsBase64(byte[] buffer, int offset, int count)
        {
            AdvanceToContentOnElement();
            return _xmlReader.ReadContentAsBase64(buffer, offset, count);
        }

        public override int ReadElementContentAsBase64(byte[] buffer, int offset, int count)
        {
            if (!_readingBinaryElement)
            {
                if (IsEmptyElement)
                {
                    Read();
                    return 0;
                }

                ReadStartElement();
                _readingBinaryElement = true;
            }

            int i = ReadContentAsBase64(buffer, offset, count);

            if (i == 0)
            {
                ReadEndElement();
                _readingBinaryElement = false;
            }

            return i;
        }

        public override int ReadElementContentAsBinHex(byte[] buffer, int offset, int count)
        {
            if (!_readingBinaryElement)
            {
                if (IsEmptyElement)
                {
                    Read();
                    return 0;
                }

                ReadStartElement();
                _readingBinaryElement = true;
            }

            int i = ReadContentAsBinHex(buffer, offset, count);

            if (i == 0)
            {
                ReadEndElement();
                _readingBinaryElement = false;
            }

            return i;
        }

        public override int ReadContentAsBinHex(byte[] buffer, int offset, int count)
        {
            AdvanceToContentOnElement();
            return _xmlReader.ReadContentAsBinHex(buffer, offset, count);
        }

        public override bool ReadContentAsBoolean()
        {
            AdvanceToContentOnElement();
            return _xmlReader.ReadContentAsBoolean();
        }

        public override int ReadContentAsChars(char[] chars, int index, int count)
        {
            AdvanceToContentOnElement();
            return _xmlReader.ReadContentAsChars(chars, index, count);
        }

        public override DateTime ReadContentAsDateTime()
        {
            AdvanceToContentOnElement();
            return _xmlReader.ReadContentAsDateTime();
        }

        public override decimal ReadContentAsDecimal()
        {
            AdvanceToContentOnElement();
            return _xmlReader.ReadContentAsDecimal();
        }

        public override double ReadContentAsDouble()
        {
            AdvanceToContentOnElement();
            return _xmlReader.ReadContentAsDouble();
        }

        public override int ReadContentAsInt()
        {
            AdvanceToContentOnElement();
            return _xmlReader.ReadContentAsInt();
        }

        public override long ReadContentAsLong()
        {
            AdvanceToContentOnElement();
            return _xmlReader.ReadContentAsLong();
        }

        public override object ReadContentAsObject()
        {
            AdvanceToContentOnElement();
            return _xmlReader.ReadContentAsObject();
        }

        public override float ReadContentAsFloat()
        {
            AdvanceToContentOnElement();
            return _xmlReader.ReadContentAsFloat();
        }

        public override string ReadContentAsString()
        {
            AdvanceToContentOnElement();
            return _xmlReader.ReadContentAsString();
        }

        public override string ReadInnerXml() => _xmlReader.ReadInnerXml();

        public override string ReadOuterXml() => _xmlReader.ReadOuterXml();

        public override ReadState ReadState
        {
            get
            {
                if (_xmlReader.ReadState != ReadState.Interactive && _infosetReader != null)
                    return _infosetReader.ReadState;

                return _xmlReader.ReadState;
            }
        }

        public override int ReadValueChunk(char[] buffer, int index, int count) => _xmlReader.ReadValueChunk(buffer, index, count);

        public override void ResolveEntity() => _xmlReader.ResolveEntity();

        public override XmlReaderSettings Settings => _xmlReader.Settings;

        public override void Skip() => _xmlReader.Skip();

        public override string this[int index] => _xmlReader[index];

        public override string this[string name] => _xmlReader[name];

        public override string this[string name, string ns] => _xmlReader[name, ns];

        public override string Value => _xmlReader.Value;

        public override Type ValueType => _xmlReader.ValueType;

        public override string XmlLang => _xmlReader.XmlLang;

        public override XmlSpace XmlSpace => _xmlReader.XmlSpace;

        public bool HasLineInfo()
        {
            if (_xmlReader.ReadState == ReadState.Closed)
                return false;

            IXmlLineInfo lineInfo = _xmlReader as IXmlLineInfo;
            if (lineInfo == null)
                return false;
            return lineInfo.HasLineInfo();
        }

        public int LineNumber
        {
            get
            {
                if (_xmlReader.ReadState == ReadState.Closed)
                    return 0;

                IXmlLineInfo lineInfo = _xmlReader as IXmlLineInfo;
                if (lineInfo == null)
                    return 0;
                return lineInfo.LineNumber;
            }
        }

        public int LinePosition
        {
            get
            {
                if (_xmlReader.ReadState == ReadState.Closed)
                    return 0;

                IXmlLineInfo lineInfo = _xmlReader as IXmlLineInfo;
                if (lineInfo == null)
                    return 0;
                return lineInfo.LinePosition;
            }
        }

        internal class MimePart
        {
            private Stream stream;
            private MimeHeaders headers;
            private byte[] buffer;
            private bool isReferencedFromInfoset;

            internal MimePart(Stream stream, MimeHeaders headers)
            {
                this.stream = stream;
                this.headers = headers;
            }

            internal Stream Stream => stream;

            internal MimeHeaders Headers => headers;

            internal bool ReferencedFromInfoset
            {
                get => isReferencedFromInfoset;
                set => isReferencedFromInfoset = value;
            }

            internal long Length => stream.CanSeek ? stream.Length : 0;

            internal byte[] GetBuffer(int maxBuffer, ref int remaining)
            {
                if (buffer == null)
                {
                    MemoryStream bufferedStream = stream.CanSeek ? new MemoryStream((int)stream.Length) : new MemoryStream();
                    int size = 256;
                    byte[] bytes = new byte[size];

                    int read = 0;

                    do
                    {
                        read = stream.Read(bytes, 0, size);
                        XmlMtomReader.DecrementBufferQuota(maxBuffer, ref remaining, read);
                        if (read > 0)
                            bufferedStream.Write(bytes, 0, read);
                    }
                    while (read > 0);

                    bufferedStream.Seek(0, SeekOrigin.Begin);
                    buffer = bufferedStream.GetBuffer();
                    stream = bufferedStream;
                }
                return buffer;
            }

            internal void Release(int maxBuffer, ref int remaining)
            {
                remaining += (int)Length;
                headers.Release(ref remaining);
            }
        }

        internal class XopIncludeReader : XmlDictionaryReader, IXmlLineInfo
        {
            private int _chunkSize = 4096;  // Just a default.  Serves as a max chunk size.
            private int _bytesRemaining;
            private MimePart _part;
            private ReadState _readState;
            private XmlDictionaryReader _parentReader;
            private string _stringValue;
            private int _stringOffset;
            private XmlNodeType _nodeType;
            private MemoryStream _binHexStream;
            private byte[] _valueBuffer;
            private int _valueOffset;
            private int _valueCount;
            private bool _finishedStream;

            public XopIncludeReader(MimePart part, XmlDictionaryReader reader)
            {
                if (part == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(part));
                if (reader == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(reader));

                _part = part;
                _parentReader = reader;
                _readState = ReadState.Initial;
                _nodeType = XmlNodeType.None;
                _chunkSize = Math.Min(reader.Quotas.MaxBytesPerRead, _chunkSize);
                _bytesRemaining = _chunkSize;
                _finishedStream = false;
            }

            public override XmlDictionaryReaderQuotas Quotas => _parentReader.Quotas;

            public override XmlNodeType NodeType => (_readState == ReadState.Interactive) ? _nodeType : _parentReader.NodeType;

            public override bool Read()
            {
                bool retVal = true;
                switch (_readState)
                {
                    case ReadState.Initial:
                        _readState = ReadState.Interactive;
                        _nodeType = XmlNodeType.Text;
                        break;
                    case ReadState.Interactive:
                        if (_finishedStream || (_bytesRemaining == _chunkSize && _stringValue == null))
                        {
                            _readState = ReadState.EndOfFile;
                            _nodeType = XmlNodeType.EndElement;
                        }
                        else
                        {
                            _bytesRemaining = _chunkSize;
                        }
                        break;
                    case ReadState.EndOfFile:
                        _nodeType = XmlNodeType.None;
                        retVal = false;
                        break;
                }
                _stringValue = null;
                _binHexStream = null;
                _valueOffset = 0;
                _valueCount = 0;
                _stringOffset = 0;
                CloseStreams();
                return retVal;
            }

            public override int ReadValueAsBase64(byte[] buffer, int offset, int count)
            {
                if (buffer == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(buffer));

                if (offset < 0)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(offset), SRCommon.ValueMustBeNonNegative));
                if (offset > buffer.Length)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(offset), SR.Format(SR.OffsetExceedsBufferSize, buffer.Length)));
                if (count < 0)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(count), SRCommon.ValueMustBeNonNegative));
                if (count > buffer.Length - offset)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(count), SR.Format(SR.SizeExceedsRemainingBufferSpace, buffer.Length - offset)));

                if (_stringValue != null)
                {
                    count = Math.Min(count, _valueCount);
                    if (count > 0)
                    {
                        Buffer.BlockCopy(_valueBuffer, _valueOffset, buffer, offset, count);
                        _valueOffset += count;
                        _valueCount -= count;
                    }
                    return count;
                }

                if (_bytesRemaining < count)
                    count = _bytesRemaining;

                int read = 0;
                if (_readState == ReadState.Interactive)
                {
                    while (read < count)
                    {
                        int actual = _part.Stream.Read(buffer, offset + read, count - read);
                        if (actual == 0)
                        {
                            _finishedStream = true;
                            break;
                        }
                        read += actual;
                    }
                }
                _bytesRemaining -= read;
                return read;
            }

            public override int ReadContentAsBase64(byte[] buffer, int offset, int count)
            {
                if (buffer == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(buffer));

                if (offset < 0)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(offset), SRCommon.ValueMustBeNonNegative));
                if (offset > buffer.Length)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(offset), SR.Format(SR.OffsetExceedsBufferSize, buffer.Length)));
                if (count < 0)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(count), SRCommon.ValueMustBeNonNegative));
                if (count > buffer.Length - offset)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(count), SR.Format(SR.SizeExceedsRemainingBufferSpace, buffer.Length - offset)));

                if (_valueCount > 0)
                {
                    count = Math.Min(count, _valueCount);
                    Buffer.BlockCopy(_valueBuffer, _valueOffset, buffer, offset, count);
                    _valueOffset += count;
                    _valueCount -= count;
                    return count;
                }

                if (_chunkSize < count)
                    count = _chunkSize;

                int read = 0;
                if (_readState == ReadState.Interactive)
                {
                    while (read < count)
                    {
                        int actual = _part.Stream.Read(buffer, offset + read, count - read);
                        if (actual == 0)
                        {
                            _finishedStream = true;
                            if (!Read())
                                break;
                        }
                        read += actual;
                    }
                }
                _bytesRemaining = _chunkSize;
                return read;
            }

            public override int ReadContentAsBinHex(byte[] buffer, int offset, int count)
            {
                if (buffer == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(buffer));

                if (offset < 0)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(offset), SRCommon.ValueMustBeNonNegative));
                if (offset > buffer.Length)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(offset), SR.Format(SR.OffsetExceedsBufferSize, buffer.Length)));
                if (count < 0)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(count), SRCommon.ValueMustBeNonNegative));
                if (count > buffer.Length - offset)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(count), SR.Format(SR.SizeExceedsRemainingBufferSpace, buffer.Length - offset)));

                if (_chunkSize < count)
                    count = _chunkSize;

                int read = 0;
                int consumed = 0;
                while (read < count)
                {
                    if (_binHexStream == null)
                    {
                        try
                        {
                            _binHexStream = new MemoryStream(new BinHexEncoding().GetBytes(Value));
                        }
                        catch (FormatException e) // Wrap format exceptions from decoding document contents
                        {
                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(e.Message, e));
                        }
                    }

                    int actual = _binHexStream.Read(buffer, offset + read, count - read);
                    if (actual == 0)
                    {
                        _finishedStream = true;
                        if (!Read())
                            break;

                        consumed = 0;
                    }

                    read += actual;
                    consumed += actual;
                }

                // Trim off the consumed chars
                if (_stringValue != null && consumed > 0)
                {
                    _stringValue = _stringValue.Substring(consumed * 2);
                    _stringOffset = Math.Max(0, _stringOffset - consumed * 2);

                    _bytesRemaining = _chunkSize;
                }
                return read;
            }

            public override int ReadValueChunk(char[] chars, int offset, int count)
            {
                if (chars == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(chars));

                if (offset < 0)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(offset), SRCommon.ValueMustBeNonNegative));
                if (offset > chars.Length)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(offset), SR.Format(SR.OffsetExceedsBufferSize, chars.Length)));
                if (count < 0)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(count), SRCommon.ValueMustBeNonNegative));
                if (count > chars.Length - offset)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(count), SR.Format(SR.SizeExceedsRemainingBufferSpace, chars.Length - offset)));

                if (_readState != ReadState.Interactive)
                    return 0;

                // Copy characters from the Value property
                string str = Value;
                count = Math.Min(_stringValue.Length - _stringOffset, count);
                if (count > 0)
                {
                    _stringValue.CopyTo(_stringOffset, chars, offset, count);
                    _stringOffset += count;
                }
                return count;
            }

            public override string Value
            {
                get
                {
                    if (_readState != ReadState.Interactive)
                        return string.Empty;

                    if (_stringValue == null)
                    {
                        // Compute the bytes to read
                        int byteCount = _bytesRemaining;
                        byteCount -= byteCount % 3;

                        // Handle trailing bytes
                        if (_valueCount > 0 && _valueOffset > 0)
                        {
                            Buffer.BlockCopy(_valueBuffer, _valueOffset, _valueBuffer, 0, _valueCount);
                            _valueOffset = 0;
                        }
                        byteCount -= _valueCount;

                        // Resize buffer if needed
                        if (_valueBuffer == null)
                        {
                            _valueBuffer = new byte[byteCount];
                        }
                        else if (_valueBuffer.Length < byteCount)
                        {
                            Array.Resize(ref _valueBuffer, byteCount);
                        }
                        byte[] buffer = _valueBuffer;

                        // Fill up the buffer
                        int offset = 0;
                        int read = 0;
                        while (byteCount > 0)
                        {
                            read = _part.Stream.Read(buffer, offset, byteCount);
                            if (read == 0)
                            {
                                _finishedStream = true;
                                break;
                            }

                            _bytesRemaining -= read;
                            _valueCount += read;
                            byteCount -= read;
                            offset += read;
                        }

                        // Convert the bytes
                        _stringValue = Convert.ToBase64String(buffer, 0, _valueCount);
                    }
                    return _stringValue;
                }
            }

            public override string ReadContentAsString()
            {
                int stringContentQuota = Quotas.MaxStringContentLength;
                StringBuilder sb = new StringBuilder();
                do
                {
                    string val = Value;
                    if (val.Length > stringContentQuota)
                        XmlExceptionHelper.ThrowMaxStringContentLengthExceeded(this, Quotas.MaxStringContentLength);
                    stringContentQuota -= val.Length;
                    sb.Append(val);
                } while (Read());
                return sb.ToString();
            }

            public override int AttributeCount => 0;

            public override string BaseURI => _parentReader.BaseURI;

            public override bool CanReadBinaryContent => true;

            public override bool CanReadValueChunk => true;

            public override bool CanResolveEntity => _parentReader.CanResolveEntity;

            public override void Close()
            {
                CloseStreams();
                _readState = ReadState.Closed;
            }

            private void CloseStreams()
            {
                if (_binHexStream != null)
                {
                    _binHexStream.Close();
                    _binHexStream = null;
                }
            }

            public override int Depth => (_readState == ReadState.Interactive) ? _parentReader.Depth + 1 : _parentReader.Depth;

            public override bool EOF => _readState == ReadState.EndOfFile;

            public override string GetAttribute(int index) => null;

            public override string GetAttribute(string name) => null;

            public override string GetAttribute(string name, string ns) => null;

            public override string GetAttribute(XmlDictionaryString localName, XmlDictionaryString ns) => null;

            public override bool HasAttributes => false;

            public override bool HasValue => _readState == ReadState.Interactive;

            public override bool IsDefault => false;

            public override bool IsEmptyElement => false;

            public override bool IsLocalName(string localName) => false;

            public override bool IsLocalName(XmlDictionaryString localName) => false;

            public override bool IsNamespaceUri(string ns) => false;

            public override bool IsNamespaceUri(XmlDictionaryString ns) => false;

            public override bool IsStartElement() => false;

            public override bool IsStartElement(string localName) => false;

            public override bool IsStartElement(string localName, string ns) => false;

            public override bool IsStartElement(XmlDictionaryString localName, XmlDictionaryString ns) => false;

            public override string LocalName => (_readState == ReadState.Interactive) ? string.Empty : _parentReader.LocalName;

            public override string LookupNamespace(string ns) => _parentReader.LookupNamespace(ns);

            public override void MoveToAttribute(int index)
            {
            }

            public override bool MoveToAttribute(string name) => false;

            public override bool MoveToAttribute(string name, string ns) => false;

            public override bool MoveToElement() => false;

            public override bool MoveToFirstAttribute() => false;

            public override bool MoveToNextAttribute() => false;

            public override string Name => (_readState == ReadState.Interactive) ? string.Empty : _parentReader.Name;

            public override string NamespaceURI => (_readState == ReadState.Interactive) ? string.Empty : _parentReader.NamespaceURI;

            public override XmlNameTable NameTable => _parentReader.NameTable;

            public override string Prefix => (_readState == ReadState.Interactive) ? string.Empty : _parentReader.Prefix;

            public override char QuoteChar => _parentReader.QuoteChar;

            public override bool ReadAttributeValue() => false;

            public override string ReadInnerXml() => ReadContentAsString();

            public override string ReadOuterXml() => ReadContentAsString();

            public override ReadState ReadState => _readState;

            public override void ResolveEntity()
            {
            }

            public override XmlReaderSettings Settings => _parentReader.Settings;

            public override void Skip() => Read();

            public override string this[int index] => null;

            public override string this[string name] => null;

            public override string this[string name, string ns] => null;

            public override string XmlLang => _parentReader.XmlLang;

            public override XmlSpace XmlSpace => _parentReader.XmlSpace;

            public override Type ValueType => (_readState == ReadState.Interactive) ? typeof(byte[]) : _parentReader.ValueType;

            bool IXmlLineInfo.HasLineInfo() => ((IXmlLineInfo)_parentReader).HasLineInfo();

            int IXmlLineInfo.LineNumber => ((IXmlLineInfo)_parentReader).LineNumber;

            int IXmlLineInfo.LinePosition => ((IXmlLineInfo)_parentReader).LinePosition;
        }
    }

    internal class MimeMessageReader
    {
        private static byte[] CRLFCRLF = new byte[] { (byte)'\r', (byte)'\n', (byte)'\r', (byte)'\n' };
        private bool getContentStreamCalled;
        private MimeHeaderReader mimeHeaderReader;
        private DelimittedStreamReader reader;

        public MimeMessageReader(Stream stream)
        {
            if (stream == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(stream));

            reader = new DelimittedStreamReader(stream);
            mimeHeaderReader = new MimeHeaderReader(reader.GetNextStream(CRLFCRLF));
        }

        public Stream GetContentStream()
        {
            if (getContentStreamCalled)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.MimeMessageGetContentStreamCalledAlready));

            mimeHeaderReader.Close();

            Stream s = reader.GetNextStream(null);

            getContentStreamCalled = true;

            return s;
        }

        public MimeHeaders ReadHeaders(int maxBuffer, ref int remaining)
        {
            MimeHeaders headers = new MimeHeaders();
            while (mimeHeaderReader.Read(maxBuffer, ref remaining))
            {
                headers.Add(mimeHeaderReader.Name, mimeHeaderReader.Value, ref remaining);
            }
            return headers;
        }
    }

    internal class MimeReader
    {
        private static byte[] CRLFCRLF = new byte[] { (byte)'\r', (byte)'\n', (byte)'\r', (byte)'\n' };
        private byte[] boundaryBytes;
        private string content;
        private Stream currentStream;
        private MimeHeaderReader mimeHeaderReader;
        private DelimittedStreamReader reader;
        private byte[] scratch = new byte[2];

        public MimeReader(Stream stream, string boundary)
        {
            if (stream == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(stream));
            if (boundary == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(boundary));

            reader = new DelimittedStreamReader(stream);
            boundaryBytes = MimeWriter.GetBoundaryBytes(boundary);

            // Need to ensure that the content begins with a CRLF, in case the
            // outer construct has consumed the trailing CRLF
            reader.Push(boundaryBytes, 0, 2);
        }

        public void Close() => reader.Close();

        /// Gets the content preceding the first part of the MIME multi-part message
        public string Preface
        {
            get
            {
                if (content == null)
                {
                    Stream s = reader.GetNextStream(boundaryBytes);
                    content = new StreamReader(s, System.Text.Encoding.ASCII, false, 256).ReadToEnd();
                    s.Close();
                    if (content == null)
                        content = string.Empty;
                }
                return content;
            }
        }

        public Stream GetContentStream()
        {
            Fx.Assert(content != null, "");

            mimeHeaderReader.Close();

            return reader.GetNextStream(boundaryBytes);
        }

        public bool ReadNextPart()
        {
            string content = Preface;

            if (currentStream != null)
            {
                currentStream.Close();
                currentStream = null;
            }

            Stream stream = reader.GetNextStream(CRLFCRLF);

            if (stream == null)
                return false;

            if (BlockRead(stream, scratch, 0, 2) == 2)
            {
                if (scratch[0] == '\r' && scratch[1] == '\n')
                {
                    if (mimeHeaderReader == null)
                        mimeHeaderReader = new MimeHeaderReader(stream);
                    else
                        mimeHeaderReader.Reset(stream);
                    return true;
                }
                else if (scratch[0] == '-' && scratch[1] == '-')
                {
                    int read = BlockRead(stream, scratch, 0, 2);

                    if (read < 2 || (scratch[0] == '\r' && scratch[1] == '\n'))
                        return false;
                }
            }

            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new FormatException(SR.MimeReaderTruncated));
        }

        public MimeHeaders ReadHeaders(int maxBuffer, ref int remaining)
        {
            MimeHeaders headers = new MimeHeaders();
            while (mimeHeaderReader.Read(maxBuffer, ref remaining))
            {
                headers.Add(mimeHeaderReader.Name, mimeHeaderReader.Value, ref remaining);
            }
            return headers;
        }

        private int BlockRead(Stream stream, byte[] buffer, int offset, int count)
        {
            int read = 0;
            do
            {
                int r = stream.Read(buffer, offset + read, count - read);
                if (r == 0)
                    break;
                read += r;
            } while (read < count);
            return read;
        }

    }

    internal class DelimittedStreamReader
    {
        private bool canGetNextStream = true;

        // used for closing the reader, and validating that only one stream can be reading at a time.
        private DelimittedReadStream currentStream;
        private byte[] delimitter;
        private byte[] matchBuffer;
        private byte[] scratch;
        private BufferedReadStream stream;

        public DelimittedStreamReader(Stream stream)
        {
            this.stream = new BufferedReadStream(stream);
        }

        public void Close() => stream.Close();

        // Closes the current stream.  If the current stream is not the same as the caller, nothing is done.
        private void Close(DelimittedReadStream caller)
        {
            if (currentStream == caller)
            {
                if (delimitter == null)
                {
                    stream.Close();
                }
                else
                {
                    if (scratch == null)
                    {
                        scratch = new byte[1024];
                    }
                    while (0 != Read(caller, scratch, 0, scratch.Length))
                        ;
                }

                currentStream = null;
            }
        }

        // Gets the next logical stream delimitted by the given sequence.
        public Stream GetNextStream(byte[] delimitter)
        {
            if (currentStream != null)
            {
                currentStream.Close();
                currentStream = null;
            }

            if (!canGetNextStream)
                return null;

            this.delimitter = delimitter;

            canGetNextStream = delimitter != null;

            currentStream = new DelimittedReadStream(this);

            return currentStream;
        }

        private enum MatchState
        {
            True,
            False,
            InsufficientData
        }

        private MatchState MatchDelimitter(byte[] buffer, int start, int end)
        {
            if (delimitter.Length > end - start)
            {
                for (int i = end - start - 1; i >= 1; i--)
                {
                    if (buffer[start + i] != delimitter[i])
                        return MatchState.False;
                }
                return MatchState.InsufficientData;
            }
            for (int i = delimitter.Length - 1; i >= 1; i--)
            {
                if (buffer[start + i] != delimitter[i])
                    return MatchState.False;
            }
            return MatchState.True;
        }

        private int ProcessRead(byte[] buffer, int offset, int read)
        {
            // nothing to process if 0 bytes were read
            if (read == 0)
                return read;

            for (int ptr = offset, end = offset + read; ptr < end; ptr++)
            {
                if (buffer[ptr] == delimitter[0])
                {
                    switch (MatchDelimitter(buffer, ptr, end))
                    {
                        case MatchState.True:
                            {
                                int actual = ptr - offset;
                                ptr += delimitter.Length;
                                stream.Push(buffer, ptr, end - ptr);
                                currentStream = null;
                                return actual;
                            }
                        case MatchState.False:
                            break;
                        case MatchState.InsufficientData:
                            {
                                int actual = ptr - offset;
                                if (actual > 0)
                                {
                                    stream.Push(buffer, ptr, end - ptr);
                                    return actual;
                                }
                                else
                                {
                                    return -1;
                                }
                            }
                    }
                }
            }
            return read;
        }

        private int Read(DelimittedReadStream caller, byte[] buffer, int offset, int count)
        {
            if (currentStream != caller)
                return 0;

            int read = stream.Read(buffer, offset, count);
            if (read == 0)
            {
                canGetNextStream = false;
                currentStream = null;
                return read;
            }

            // If delimitter is null, read until the underlying stream returns 0 bytes
            if (delimitter == null)
                return read;

            // Scans the read data for the delimitter. If found, the number of bytes read are adjusted
            // to account for the number of bytes up to but not including the delimitter.
            int actual = ProcessRead(buffer, offset, read);

            if (actual < 0)
            {
                if (matchBuffer == null || matchBuffer.Length < delimitter.Length - read)
                    matchBuffer = new byte[delimitter.Length - read];

                int matched = stream.ReadBlock(matchBuffer, 0, delimitter.Length - read);

                if (MatchRemainder(read, matched))
                {
                    currentStream = null;
                    actual = 0;
                }
                else
                {
                    stream.Push(matchBuffer, 0, matched);

                    int i = 1;
                    for (; i < read; i++)
                    {
                        if (buffer[i] == delimitter[0])
                            break;
                    }

                    if (i < read)
                        stream.Push(buffer, offset + i, read - i);

                    actual = i;
                }
            }

            return actual;
        }

        private bool MatchRemainder(int start, int count)
        {
            if (start + count != delimitter.Length)
                return false;

            for (count--; count >= 0; count--)
            {
                if (delimitter[start + count] != matchBuffer[count])
                    return false;
            }
            return true;
        }

        internal void Push(byte[] buffer, int offset, int count) => stream.Push(buffer, offset, count);

        private class DelimittedReadStream : Stream
        {
            private DelimittedStreamReader reader;

            public DelimittedReadStream(DelimittedStreamReader reader)
            {
                if (reader == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(reader));

                this.reader = reader;
            }

            public override bool CanRead => true;

            public override bool CanSeek => false;

            public override bool CanWrite => false;

            public override long Length => throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.SeekNotSupportedOnStream, GetType().FullName)));

            public override long Position
            {
                get => throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.SeekNotSupportedOnStream, GetType().FullName)));
                set => throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.SeekNotSupportedOnStream, GetType().FullName)));
            }

            public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state) => throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.WriteNotSupportedOnStream, GetType().FullName)));

            public override void Close() => reader.Close(this);

            public override void EndWrite(IAsyncResult asyncResult) => throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.WriteNotSupportedOnStream, GetType().FullName)));

            public override void Flush() => throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.WriteNotSupportedOnStream, GetType().FullName)));

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (buffer == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(buffer));

                if (offset < 0)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(offset), SRCommon.ValueMustBeNonNegative));
                if (offset > buffer.Length)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(offset), SR.Format(SR.OffsetExceedsBufferSize, buffer.Length)));
                if (count < 0)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(count), SRCommon.ValueMustBeNonNegative));
                if (count > buffer.Length - offset)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(count), SR.Format(SR.SizeExceedsRemainingBufferSpace, buffer.Length - offset)));

                return reader.Read(this, buffer, offset, count);
            }

            public override long Seek(long offset, SeekOrigin origin) => throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.SeekNotSupportedOnStream, GetType().FullName)));

            public override void SetLength(long value) => throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.WriteNotSupportedOnStream, GetType().FullName)));

            public override void Write(byte[] buffer, int offset, int count) => throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.WriteNotSupportedOnStream, GetType().FullName)));
        }

    }

    internal class MimeHeaders
    {
        private static class Constants
        {
            public const string ContentTransferEncoding = "content-transfer-encoding";
            public const string ContentID = "content-id";
            public const string ContentType = "content-type";
            public const string MimeVersion = "mime-version";
        }

        private Dictionary<string, MimeHeader> headers = new Dictionary<string, MimeHeader>();

        public MimeHeaders()
        {
        }

        public ContentTypeHeader ContentType
        {
            get
            {
                MimeHeader header;
                if (headers.TryGetValue(Constants.ContentType, out header))
                    return header as ContentTypeHeader;
                return null;
            }
        }

        public ContentIDHeader ContentID
        {
            get
            {
                MimeHeader header;
                if (headers.TryGetValue(Constants.ContentID, out header))
                    return header as ContentIDHeader;
                return null;
            }
        }

        public ContentTransferEncodingHeader ContentTransferEncoding
        {
            get
            {
                MimeHeader header;
                if (headers.TryGetValue(Constants.ContentTransferEncoding, out header))
                    return header as ContentTransferEncodingHeader;
                return null;
            }
        }

        public MimeVersionHeader MimeVersion
        {
            get
            {
                MimeHeader header;
                if (headers.TryGetValue(Constants.MimeVersion, out header))
                    return header as MimeVersionHeader;
                return null;
            }
        }

        public void Add(string name, string value, ref int remaining)
        {
            if (name == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(name));

            if (value == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));

            switch (name)
            {
                case Constants.ContentType:
                    Add(new ContentTypeHeader(value));
                    break;
                case Constants.ContentID:
                    Add(new ContentIDHeader(name, value));
                    break;
                case Constants.ContentTransferEncoding:
                    Add(new ContentTransferEncodingHeader(value));
                    break;
                case Constants.MimeVersion:
                    Add(new MimeVersionHeader(value));
                    break;

                // Skip any fields that are not recognized
                // Content-description is currently not stored since it is not used
                default:
                    remaining += value.Length * sizeof(char);
                    break;
            }
            remaining += name.Length * sizeof(char);
        }

        public void Add(MimeHeader header)
        {
            if (header == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(header));

            MimeHeader existingHeader;
            if (headers.TryGetValue(header.Name, out existingHeader))
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new FormatException(SR.Format(SR.MimeReaderHeaderAlreadyExists, header.Name)));
            else
                headers.Add(header.Name, header);
        }

        public void Release(ref int remaining)
        {
            foreach (MimeHeader header in headers.Values)
            {
                remaining += header.Value.Length * sizeof(char);
            }
        }

    }

    internal class MimeHeader
    {
        private string name;
        private string value;

        public MimeHeader(string name, string value)
        {
            if (name == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(name));

            this.name = name;
            this.value = value;
        }

        public string Name => name;

        public string Value => value;
    }

    internal class ContentTypeHeader : MimeHeader
    {
        public static readonly ContentTypeHeader Default = new ContentTypeHeader("application/octet-stream");

        public ContentTypeHeader(string value)
            : base("content-type", value)
        {
        }

        private string mediaType;
        private string subType;
        private Dictionary<string, string> parameters;

        public string MediaType
        {
            get
            {
                if (mediaType == null && Value != null)
                    ParseValue();

                return mediaType;
            }
        }

        public string MediaSubtype
        {
            get
            {
                if (subType == null && Value != null)
                    ParseValue();

                return subType;
            }
        }

        public Dictionary<string, string> Parameters
        {
            get
            {
                if (parameters == null)
                {
                    if (Value != null)
                        ParseValue();
                    else
                        parameters = new Dictionary<string, string>();
                }
                return parameters;
            }
        }

        private void ParseValue()
        {
            if (parameters == null)
            {
                int offset = 0;
                parameters = new Dictionary<string, string>();
                mediaType = MailBnfHelper.ReadToken(Value, ref offset, null);
                if (offset >= Value.Length || Value[offset++] != '/')
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new FormatException(SR.MimeContentTypeHeaderInvalid));
                subType = MailBnfHelper.ReadToken(Value, ref offset, null);

                while (MailBnfHelper.SkipCFWS(Value, ref offset))
                {
                    if (offset >= Value.Length || Value[offset++] != ';')
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new FormatException(SR.MimeContentTypeHeaderInvalid));

                    if (!MailBnfHelper.SkipCFWS(Value, ref offset))
                        break;

                    string paramAttribute = MailBnfHelper.ReadParameterAttribute(Value, ref offset, null);
                    if (paramAttribute == null || offset >= Value.Length || Value[offset++] != '=')
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new FormatException(SR.MimeContentTypeHeaderInvalid));
                    string paramValue = MailBnfHelper.ReadParameterValue(Value, ref offset, null);

                    parameters.Add(paramAttribute.ToLowerInvariant(), paramValue);
                }

                if (parameters.ContainsKey(MtomGlobals.StartInfoParam))
                {
                    // This allows us to maintain back compat with Orcas clients while allowing clients
                    // following the spec (with action inside start-info) to interop with RFC 2387
                    string startInfo = parameters[MtomGlobals.StartInfoParam];

                    // we're only interested in finding the action here - skipping past the content type to the first ;
                    int startInfoOffset = startInfo.IndexOf(';');
                    if (startInfoOffset > -1)
                    {
                        // keep going through the start-info string until we've reached the end of the stream
                        while (MailBnfHelper.SkipCFWS(startInfo, ref startInfoOffset))
                        {
                            // after having read through an attribute=value pair, we always expect to be at a ;
                            if (startInfo[startInfoOffset] == ';')
                            {
                                startInfoOffset++;
                            }
                            else
                            {
                                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new FormatException(SR.MimeContentTypeHeaderInvalid));
                            }
                            string paramAttribute = MailBnfHelper.ReadParameterAttribute(startInfo, ref startInfoOffset, null);
                            if (paramAttribute == null || startInfoOffset >= startInfo.Length || startInfo[startInfoOffset++] != '=')
                                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new FormatException(SR.MimeContentTypeHeaderInvalid));
                            string paramValue = MailBnfHelper.ReadParameterValue(startInfo, ref startInfoOffset, null);

                            if (paramAttribute == MtomGlobals.ActionParam)
                            {
                                parameters[MtomGlobals.ActionParam] = paramValue;
                            }
                        }
                    }
                }

            }
        }
    }

    internal enum ContentTransferEncoding
    {
        SevenBit,
        EightBit,
        Binary,
        Other,
        Unspecified
    }

    internal class ContentTransferEncodingHeader : MimeHeader
    {
        private ContentTransferEncoding contentTransferEncoding;
        private string contentTransferEncodingValue;

        public static readonly ContentTransferEncodingHeader Binary = new ContentTransferEncodingHeader(ContentTransferEncoding.Binary, "binary");
        public static readonly ContentTransferEncodingHeader EightBit = new ContentTransferEncodingHeader(ContentTransferEncoding.EightBit, "8bit");
        public static readonly ContentTransferEncodingHeader SevenBit = new ContentTransferEncodingHeader(ContentTransferEncoding.SevenBit, "7bit");

        public ContentTransferEncodingHeader(string value)
            : base("content-transfer-encoding", value.ToLowerInvariant())
        {
        }

        public ContentTransferEncodingHeader(ContentTransferEncoding contentTransferEncoding, string value)
            : base("content-transfer-encoding", null)
        {
            this.contentTransferEncoding = contentTransferEncoding;
            contentTransferEncodingValue = value;
        }

        public ContentTransferEncoding ContentTransferEncoding
        {
            get
            {
                ParseValue();
                return contentTransferEncoding;
            }
        }

        public string ContentTransferEncodingValue
        {
            get
            {
                ParseValue();
                return contentTransferEncodingValue;
            }
        }

        private void ParseValue()
        {
            if (contentTransferEncodingValue == null)
            {
                int offset = 0;
                contentTransferEncodingValue = (Value.Length == 0) ? Value : ((Value[0] == '"') ? MailBnfHelper.ReadQuotedString(Value, ref offset, null) : MailBnfHelper.ReadToken(Value, ref offset, null));
                switch (contentTransferEncodingValue)
                {
                    case "7bit":
                        contentTransferEncoding = ContentTransferEncoding.SevenBit;
                        break;
                    case "8bit":
                        contentTransferEncoding = ContentTransferEncoding.EightBit;
                        break;
                    case "binary":
                        contentTransferEncoding = ContentTransferEncoding.Binary;
                        break;
                    default:
                        contentTransferEncoding = ContentTransferEncoding.Other;
                        break;
                }
            }
        }
    }

    internal class ContentIDHeader : MimeHeader
    {
        public ContentIDHeader(string name, string value)
            : base(name, value)
        {
        }
    }

    internal class MimeVersionHeader : MimeHeader
    {
        public static readonly MimeVersionHeader Default = new MimeVersionHeader("1.0");

        public MimeVersionHeader(string value)
            : base("mime-version", value)
        {
        }

        private string version;

        public string Version
        {
            get
            {
                if (version == null && Value != null)
                    ParseValue();
                return version;
            }
        }

        private void ParseValue()
        {
            // shortcut for the most common case.
            if (Value == "1.0")
            {
                version = "1.0";
            }
            else
            {
                int offset = 0;

                if (!MailBnfHelper.SkipCFWS(Value, ref offset))
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new FormatException(SR.MimeVersionHeaderInvalid));

                StringBuilder builder = new StringBuilder();
                MailBnfHelper.ReadDigits(Value, ref offset, builder);

                if ((!MailBnfHelper.SkipCFWS(Value, ref offset) || offset >= Value.Length || Value[offset++] != '.') || !MailBnfHelper.SkipCFWS(Value, ref offset))
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new FormatException(SR.MimeVersionHeaderInvalid));

                builder.Append('.');

                MailBnfHelper.ReadDigits(Value, ref offset, builder);

                version = builder.ToString();
            }
        }
    }

    internal class MimeHeaderReader
    {
        private enum ReadState
        {
            ReadName,
            SkipWS,
            ReadValue,
            ReadLF,
            ReadWS,
            EOF
        }

        private string value;
        private byte[] buffer = new byte[1024];
        private int maxOffset;
        private string name;
        private int offset;
        private ReadState readState = ReadState.ReadName;
        private Stream stream;

        public MimeHeaderReader(Stream stream)
        {
            if (stream == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(stream));

            this.stream = stream;
        }

        public string Value => value;

        public string Name => name;

        public void Close()
        {
            stream.Close();
            readState = ReadState.EOF;
        }

        public bool Read(int maxBuffer, ref int remaining)
        {
            name = null;
            value = null;

            while (readState != ReadState.EOF)
            {
                if (offset == maxOffset)
                {
                    maxOffset = stream.Read(buffer, 0, buffer.Length);
                    offset = 0;
                    if (BufferEnd())
                        break;
                }
                if (ProcessBuffer(maxBuffer, ref remaining))
                    break;
            }

            return value != null;
        }

        private bool ProcessBuffer(int maxBuffer, ref int remaining)
        {
            unsafe
            {
                fixed (byte* pBuffer = buffer)
                {
                    byte* start = pBuffer + offset;
                    byte* end = pBuffer + maxOffset;
                    byte* ptr = start;

                    switch (readState)
                    {
                        case ReadState.ReadName:
                            for (; ptr < end; ptr++)
                            {
                                if (*ptr == ':')
                                {
                                    AppendName(new string((sbyte*)start, 0, (int)(ptr - start)), maxBuffer, ref remaining);
                                    ptr++;
                                    goto case ReadState.SkipWS;
                                }
                                else
                                {
                                    // convert to lower case up front.
                                    if (*ptr >= 'A' && *ptr <= 'Z')
                                    {
                                        *ptr += 'a' - 'A';
                                    }
                                    else if (*ptr < 33 || *ptr > 126)
                                    {
                                        if (name == null && *ptr == (byte)'\r')
                                        {
                                            ptr++;
                                            if (ptr >= end || *ptr != '\n')
                                                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new FormatException(SR.MimeReaderMalformedHeader));
                                            goto case ReadState.EOF;
                                        }

                                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new FormatException(SR.Format(SR.MimeHeaderInvalidCharacter, (char)(*ptr), ((int)(*ptr)).ToString("X", CultureInfo.InvariantCulture))));
                                    }
                                }
                            }
                            AppendName(new string((sbyte*)start, 0, (int)(ptr - start)), maxBuffer, ref remaining);
                            readState = ReadState.ReadName;
                            break;
                        case ReadState.SkipWS:
                            for (; ptr < end; ptr++)
                                if (*ptr != (byte)'\t' && *ptr != ' ')
                                    goto case ReadState.ReadValue;
                            readState = ReadState.SkipWS;
                            break;
                        case ReadState.ReadValue:
                            start = ptr;
                            for (; ptr < end; ptr++)
                            {
                                if (*ptr == (byte)'\r')
                                {
                                    AppendValue(new string((sbyte*)start, 0, (int)(ptr - start)), maxBuffer, ref remaining);
                                    ptr++;
                                    goto case ReadState.ReadLF;
                                }
                                else if (*ptr == (byte)'\n')
                                {
                                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new FormatException(SR.MimeReaderMalformedHeader));
                                }
                            }
                            AppendValue(new string((sbyte*)start, 0, (int)(ptr - start)), maxBuffer, ref remaining);
                            readState = ReadState.ReadValue;
                            break;
                        case ReadState.ReadLF:
                            if (ptr < end)
                            {
                                if (*ptr != (byte)'\n')
                                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new FormatException(SR.MimeReaderMalformedHeader));
                                ptr++;
                                goto case ReadState.ReadWS;
                            }
                            readState = ReadState.ReadLF;
                            break;
                        case ReadState.ReadWS:
                            if (ptr < end)
                            {
                                if (*ptr != (byte)' ' && *ptr != (byte)'\t')
                                {
                                    readState = ReadState.ReadName;
                                    offset = (int)(ptr - pBuffer);
                                    return true;
                                }
                                goto case ReadState.ReadValue;
                            }
                            readState = ReadState.ReadWS;
                            break;
                        case ReadState.EOF:
                            readState = ReadState.EOF;
                            offset = (int)(ptr - pBuffer);
                            return true;
                    }
                    offset = (int)(ptr - pBuffer);
                }
            }
            return false;
        }

        private bool BufferEnd()
        {
            if (maxOffset == 0)
            {
                if (readState != ReadState.ReadWS && readState != ReadState.ReadValue)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new FormatException(SR.MimeReaderMalformedHeader));

                readState = ReadState.EOF;
                return true;
            }
            return false;
        }

        // Resets the mail field reader to the new stream to reuse buffers
        public void Reset(Stream stream)
        {
            if (stream == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(stream));

            if (readState != ReadState.EOF)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.MimeReaderResetCalledBeforeEOF));

            this.stream = stream;
            readState = ReadState.ReadName;
            maxOffset = 0;
            offset = 0;
        }

        // helper methods

        private void AppendValue(string value, int maxBuffer, ref int remaining)
        {
            XmlMtomReader.DecrementBufferQuota(maxBuffer, ref remaining, value.Length * sizeof(char));
            if (this.value == null)
                this.value = value;
            else
                this.value += value;
        }

        private void AppendName(string value, int maxBuffer, ref int remaining)
        {
            XmlMtomReader.DecrementBufferQuota(maxBuffer, ref remaining, value.Length * sizeof(char));
            if (name == null)
                name = value;
            else
                name += value;
        }

    }

    internal class BufferedReadStream : Stream
    {
        private Stream stream;
        private byte[] storedBuffer;
        private int storedLength;
        private int storedOffset;
        private bool readMore;

        public BufferedReadStream(Stream stream)
            : this(stream, false)
        {
        }

        public BufferedReadStream(Stream stream, bool readMore)
        {
            if (stream == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(stream));

            this.stream = stream;
            this.readMore = readMore;
        }

        public override bool CanWrite => false;

        public override bool CanSeek => false;

        public override bool CanRead => stream.CanRead;

        public override long Length => throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.SeekNotSupportedOnStream, stream.GetType().FullName)));

        public override long Position
        {
            get => throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.SeekNotSupportedOnStream, stream.GetType().FullName)));
            set => throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.SeekNotSupportedOnStream, stream.GetType().FullName)));
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            if (!CanRead)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.ReadNotSupportedOnStream, stream.GetType().FullName)));

            return stream.BeginRead(buffer, offset, count, callback, state);
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state) => throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.WriteNotSupportedOnStream, stream.GetType().FullName)));

        public override void Close() => stream.Close();

        public override int EndRead(IAsyncResult asyncResult)
        {
            if (!CanRead)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.ReadNotSupportedOnStream, stream.GetType().FullName)));

            return stream.EndRead(asyncResult);
        }

        public override void EndWrite(IAsyncResult asyncResult) => throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.WriteNotSupportedOnStream, stream.GetType().FullName)));

        public override void Flush() => stream.Flush();

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (!CanRead)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.ReadNotSupportedOnStream, stream.GetType().FullName)));

            if (buffer == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(buffer));

            if (offset < 0)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(offset), SRCommon.ValueMustBeNonNegative));
            if (offset > buffer.Length)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(offset), SR.Format(SR.OffsetExceedsBufferSize, buffer.Length)));
            if (count < 0)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(count), SRCommon.ValueMustBeNonNegative));
            if (count > buffer.Length - offset)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(count), SR.Format(SR.SizeExceedsRemainingBufferSpace, buffer.Length - offset)));

            int read = 0;
            if (storedOffset < storedLength)
            {
                read = Math.Min(count, storedLength - storedOffset);
                Buffer.BlockCopy(storedBuffer, storedOffset, buffer, offset, read);
                storedOffset += read;
                if (read == count || !readMore)
                    return read;
                offset += read;
                count -= read;
            }
            return read + stream.Read(buffer, offset, count);
        }

        public override int ReadByte()
        {
            if (storedOffset < storedLength)
                return (int)storedBuffer[storedOffset++];
            else
                return base.ReadByte();
        }

        public int ReadBlock(byte[] buffer, int offset, int count)
        {
            int read;
            int total = 0;
            while (total < count && (read = Read(buffer, offset + total, count - total)) != 0)
            {
                total += read;
            }
            return total;
        }

        public void Push(byte[] buffer, int offset, int count)
        {
            if (count == 0)
                return;

            if (storedOffset == storedLength)
            {
                if (storedBuffer == null || storedBuffer.Length < count)
                    storedBuffer = new byte[count];
                storedOffset = 0;
                storedLength = count;
            }
            else
            {
                // if there's room to just insert before existing data
                if (count <= storedOffset)
                    storedOffset -= count;
                // if there's room in the buffer but need to shift things over
                else if (count <= storedBuffer.Length - storedLength + storedOffset)
                {
                    Buffer.BlockCopy(storedBuffer, storedOffset, storedBuffer, count, storedLength - storedOffset);
                    storedLength += count - storedOffset;
                    storedOffset = 0;
                }
                else
                {
                    byte[] newBuffer = new byte[count + storedLength - storedOffset];
                    Buffer.BlockCopy(storedBuffer, storedOffset, newBuffer, count, storedLength - storedOffset);
                    storedLength += count - storedOffset;
                    storedOffset = 0;
                    storedBuffer = newBuffer;
                }
            }
            Buffer.BlockCopy(buffer, offset, storedBuffer, storedOffset, count);
        }

        public override long Seek(long offset, SeekOrigin origin) => throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.SeekNotSupportedOnStream, stream.GetType().FullName)));

        public override void SetLength(long value) => throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.SeekNotSupportedOnStream, stream.GetType().FullName)));

        public override void Write(byte[] buffer, int offset, int count) => throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new NotSupportedException(SR.Format(SR.WriteNotSupportedOnStream, stream.GetType().FullName)));
    }

    internal static class MailBnfHelper
    {
        private static bool[] s_fqtext = new bool[128];
        private static bool[] s_ttext = new bool[128];
        private static bool[] s_digits = new bool[128];
        private static bool[] s_boundary = new bool[128];

        static MailBnfHelper()
        {
            // fqtext = %d1-9 / %d11 / %d12 / %d14-33 / %d35-91 / %d93-127
            for (int i = 1; i <= 9; i++)
            { s_fqtext[i] = true; }
            s_fqtext[11] = true;
            s_fqtext[12] = true;
            for (int i = 14; i <= 33; i++)
            { s_fqtext[i] = true; }
            for (int i = 35; i <= 91; i++)
            { s_fqtext[i] = true; }
            for (int i = 93; i <= 127; i++)
            { s_fqtext[i] = true; }

            // ttext = %d33-126 except '()<>@,;:\"/[]?='
            for (int i = 33; i <= 126; i++)
            { s_ttext[i] = true; }
            s_ttext['('] = false;
            s_ttext[')'] = false;
            s_ttext['<'] = false;
            s_ttext['>'] = false;
            s_ttext['@'] = false;
            s_ttext[','] = false;
            s_ttext[';'] = false;
            s_ttext[':'] = false;
            s_ttext['\\'] = false;
            s_ttext['"'] = false;
            s_ttext['/'] = false;
            s_ttext['['] = false;
            s_ttext[']'] = false;
            s_ttext['?'] = false;
            s_ttext['='] = false;

            // digits = %d48-57
            for (int i = 48; i <= 57; i++)
                s_digits[i] = true;

            // boundary = DIGIT / ALPHA / "'" / "(" / ")" / "+" / "_" / "," / "-" / "." / "/" / ":" / "=" / "?" / " "
            // cannot end with " "
            for (int i = '0'; i <= '9'; i++)
            { s_boundary[i] = true; }
            for (int i = 'A'; i <= 'Z'; i++)
            { s_boundary[i] = true; }
            for (int i = 'a'; i <= 'z'; i++)
            { s_boundary[i] = true; }
            s_boundary['\''] = true;
            s_boundary['('] = true;
            s_boundary[')'] = true;
            s_boundary['+'] = true;
            s_boundary['_'] = true;
            s_boundary[','] = true;
            s_boundary['-'] = true;
            s_boundary['.'] = true;
            s_boundary['/'] = true;
            s_boundary[':'] = true;
            s_boundary['='] = true;
            s_boundary['?'] = true;
            s_boundary[' '] = true;
        }

        public static bool SkipCFWS(string data, ref int offset)
        {
            int comments = 0;
            for (; offset < data.Length; offset++)
            {
                if (data[offset] > 127)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new FormatException(SR.Format(SR.MimeHeaderInvalidCharacter, data[offset], ((int)data[offset]).ToString("X", CultureInfo.InvariantCulture))));
                else if (data[offset] == '\\' && comments > 0)
                    offset += 2;
                else if (data[offset] == '(')
                    comments++;
                else if (data[offset] == ')')
                    comments--;
                else if (data[offset] != ' ' && data[offset] != '\t' && comments == 0)
                    return true;
            }
            return false;
        }

        public static string ReadQuotedString(string data, ref int offset, StringBuilder builder)
        {
            // assume first char is the opening quote
            int start = ++offset;
            StringBuilder localBuilder = (builder != null ? builder : new StringBuilder());
            for (; offset < data.Length; offset++)
            {
                if (data[offset] == '\\')
                {
                    localBuilder.Append(data, start, offset - start);
                    start = ++offset;
                    continue;
                }
                else if (data[offset] == '"')
                {
                    localBuilder.Append(data, start, offset - start);
                    offset++;
                    return (builder != null ? null : localBuilder.ToString());
                }
                else if (!(data[offset] < s_fqtext.Length && s_fqtext[data[offset]]))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new FormatException(SR.Format(SR.MimeHeaderInvalidCharacter, data[offset], ((int)data[offset]).ToString("X", CultureInfo.InvariantCulture))));
                }
            }
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new FormatException(SR.MimeReaderMalformedHeader));
        }

        public static string ReadParameterAttribute(string data, ref int offset, StringBuilder builder)
        {
            if (!SkipCFWS(data, ref offset))
                return null;

            return ReadToken(data, ref offset, null);
        }

        public static string ReadParameterValue(string data, ref int offset, StringBuilder builder)
        {
            if (!SkipCFWS(data, ref offset))
                return string.Empty;

            if (offset < data.Length && data[offset] == '"')
                return ReadQuotedString(data, ref offset, builder);
            else
                return ReadToken(data, ref offset, builder);
        }

        public static string ReadToken(string data, ref int offset, StringBuilder builder)
        {
            int start = offset;
            for (; offset < data.Length; offset++)
            {
                if (data[offset] > s_ttext.Length)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new FormatException(SR.Format(SR.MimeHeaderInvalidCharacter, data[offset], ((int)data[offset]).ToString("X", CultureInfo.InvariantCulture))));
                }
                else if (!s_ttext[data[offset]])
                {
                    break;
                }
            }
            return data.Substring(start, offset - start);
        }

        public static string ReadDigits(string data, ref int offset, StringBuilder builder)
        {
            int start = offset;
            StringBuilder localBuilder = (builder != null ? builder : new StringBuilder());
            for (; offset < data.Length && data[offset] < s_digits.Length && s_digits[data[offset]]; offset++)
                ;
            localBuilder.Append(data, start, offset - start);
            return (builder != null ? null : localBuilder.ToString());
        }

        public static bool IsValidMimeBoundary(string data)
        {
            int length = (data == null) ? 0 : data.Length;
            if (length == 0 || length > 70 || data[length - 1] == ' ')
                return false;

            for (int i = 0; i < length; i++)
            {
                if (!(data[i] < s_boundary.Length && s_boundary[data[i]]))
                    return false;
            }

            return true;
        }
    }
}
