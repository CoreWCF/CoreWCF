// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.XPath;

namespace CoreWCF.Xml
{
    internal interface IXmlMtomWriterInitializer
    {
        void SetOutput(Stream stream, Encoding encoding, int maxSizeInBytes, string startInfo, string boundary, string startUri, bool writeMessageHeaders, bool ownsStream);
    }

    internal class XmlMtomWriter : XmlDictionaryWriter, IXmlMtomWriterInitializer
    {
        public static XmlDictionaryWriter Create(Stream stream, Encoding encoding, int maxSizeInBytes, string startInfo)
        {
            return Create(stream, encoding, maxSizeInBytes, startInfo, null, null, true, true);
        }

        public static XmlDictionaryWriter Create(Stream stream, Encoding encoding, int maxSizeInBytes, string startInfo, string boundary, string startUri, bool writeMessageHeaders, bool ownsStream)
        {
            XmlMtomWriter writer = new XmlMtomWriter();
            writer.SetOutput(stream, encoding, maxSizeInBytes, startInfo, boundary, startUri, writeMessageHeaders, ownsStream);
            return writer;
        }

        // Maximum number of bytes that are inlined as base64 data without being MTOM-optimized as xop:Include
        private const int MaxInlinedBytes = 767;  // 768 will be the first MIMEd length

        private int _maxSizeInBytes;
        private XmlDictionaryWriter _writer;
        private XmlDictionaryWriter _infosetWriter;
        private MimeWriter _mimeWriter;
        private Encoding _encoding;
        private bool _isUTF8;
        private string _contentID;
        private string _contentType;
        private string _initialContentTypeForRootPart;
        private string _initialContentTypeForMimeMessage;
        private MemoryStream _contentTypeStream;
        private List<MimePart> _mimeParts;
        private IList<MtomBinaryData> _binaryDataChunks;
        private int _depth;
        private int _totalSizeOfMimeParts;
        private int _sizeOfBufferedBinaryData;
        private char[] _chars;
        private byte[] _bytes;
        private bool _isClosed;
        private bool _ownsStream;

        public XmlMtomWriter()
        {
        }

        public void SetOutput(Stream stream, Encoding encoding, int maxSizeInBytes, string startInfo, string boundary, string startUri, bool writeMessageHeaders, bool ownsStream)
        {
            if (encoding == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(encoding));
            if (maxSizeInBytes < 0)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(maxSizeInBytes), SR.ValueMustBeNonNegative));
            _maxSizeInBytes = maxSizeInBytes;
            _encoding = encoding;
            _isUTF8 = IsUTF8Encoding(encoding);
            Initialize(stream, startInfo, boundary, startUri, writeMessageHeaders, ownsStream);
        }

        private XmlDictionaryWriter Writer
        {
            get
            {
                if (!IsInitialized)
                {
                    Initialize();
                }
                return _writer;
            }
        }

        private bool IsInitialized
        {
            get { return (_initialContentTypeForRootPart == null); }
        }

        private void Initialize(Stream stream, string startInfo, string boundary, string startUri, bool writeMessageHeaders, bool ownsStream)
        {
            if (stream == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(stream));
            if (startInfo == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(startInfo));
            if (boundary == null)
                boundary = GetBoundaryString();
            if (startUri == null)
                startUri = GenerateUriForMimePart(0);
            if (!MailBnfHelper.IsValidMimeBoundary(boundary))
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentException(SR.Format(SR.MtomBoundaryInvalid, boundary), nameof(boundary)));

            _ownsStream = ownsStream;
            _isClosed = false;
            _depth = 0;
            _totalSizeOfMimeParts = 0;
            _sizeOfBufferedBinaryData = 0;
            _binaryDataChunks = null;
            _contentType = null;
            _contentTypeStream = null;
            _contentID = startUri;
            if (_mimeParts != null)
                _mimeParts.Clear();
            _mimeWriter = new MimeWriter(stream, boundary);
            _initialContentTypeForRootPart = GetContentTypeForRootMimePart(_encoding, startInfo);
            if (writeMessageHeaders)
                _initialContentTypeForMimeMessage = GetContentTypeForMimeMessage(boundary, startUri, startInfo);
        }

        private void Initialize()
        {
            if (_isClosed)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.XmlWriterClosed));

            if (_initialContentTypeForRootPart != null)
            {
                if (_initialContentTypeForMimeMessage != null)
                {
                    _mimeWriter.StartPreface();
                    _mimeWriter.WriteHeader(MimeGlobals.MimeVersionHeader, MimeGlobals.DefaultVersion);
                    _mimeWriter.WriteHeader(MimeGlobals.ContentTypeHeader, _initialContentTypeForMimeMessage);
                    _initialContentTypeForMimeMessage = null;
                }

                WriteMimeHeaders(_contentID, _initialContentTypeForRootPart, _isUTF8 ? MimeGlobals.Encoding8bit : MimeGlobals.EncodingBinary);

                Stream infosetContentStream = _mimeWriter.GetContentStreamAsync().GetAwaiter().GetResult();
                IXmlTextWriterInitializer initializer = _writer as IXmlTextWriterInitializer;
                if (initializer == null)
                    _writer = XmlDictionaryWriter.CreateTextWriter(infosetContentStream, _encoding, _ownsStream);
                else
                    initializer.SetOutput(infosetContentStream, _encoding, _ownsStream);

                _contentID = null;
                _initialContentTypeForRootPart = null;
            }
        }

        private static string GetBoundaryString()
        {
            return MimeBoundaryGenerator.Next();
        }

        internal static bool IsUTF8Encoding(Encoding encoding)
        {
            return encoding.WebName == "utf-8";
        }

        private static string GetContentTypeForMimeMessage(string boundary, string startUri, string startInfo)
        {
            StringBuilder contentTypeBuilder = new StringBuilder(
                string.Format(CultureInfo.InvariantCulture, "{0}/{1};{2}=\"{3}\";{4}=\"{5}\"",
                    MtomGlobals.MediaType, MtomGlobals.MediaSubtype,
                    MtomGlobals.TypeParam, MtomGlobals.XopType,
                    MtomGlobals.BoundaryParam, boundary));

            if (startUri != null && startUri.Length > 0)
                contentTypeBuilder.AppendFormat(CultureInfo.InvariantCulture, ";{0}=\"<{1}>\"", MtomGlobals.StartParam, startUri);

            if (startInfo != null && startInfo.Length > 0)
                contentTypeBuilder.AppendFormat(CultureInfo.InvariantCulture, ";{0}=\"{1}\"", MtomGlobals.StartInfoParam, startInfo);

            return contentTypeBuilder.ToString();
        }

        private static string GetContentTypeForRootMimePart(Encoding encoding, string startInfo)
        {
            string contentType = string.Format(CultureInfo.InvariantCulture, "{0};{1}={2}", MtomGlobals.XopType, MtomGlobals.CharsetParam, CharSet(encoding));

            if (startInfo != null)
                contentType = string.Format(CultureInfo.InvariantCulture, "{0};{1}=\"{2}\"", contentType, MtomGlobals.TypeParam, startInfo);

            return contentType;
        }

        private static string CharSet(Encoding enc)
        {
            string name = enc.WebName;
            if (string.Compare(name, Encoding.UTF8.WebName, StringComparison.OrdinalIgnoreCase) == 0)
                return name;
            if (string.Compare(name, Encoding.Unicode.WebName, StringComparison.OrdinalIgnoreCase) == 0)
                return "utf-16LE";
            if (string.Compare(name, Encoding.BigEndianUnicode.WebName, StringComparison.OrdinalIgnoreCase) == 0)
                return "utf-16BE";
            return name;
        }

        public override void WriteStartElement(string prefix, string localName, string ns)
        {
            WriteBase64InlineIfPresent();
            ThrowIfElementIsXOPInclude(prefix, localName, ns);
            Writer.WriteStartElement(prefix, localName, ns);
            _depth++;
        }

        public override async Task WriteStartElementAsync(string prefix, string localName, string ns)
        {
            await WriteBase64InlineIfPresentAsync();
            ThrowIfElementIsXOPInclude(prefix, localName, ns);
            await Writer.WriteStartElementAsync(prefix, localName, ns);
            _depth++;
        }

        public override void WriteStartElement(string prefix, XmlDictionaryString localName, XmlDictionaryString ns)
        {
            if (localName == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(localName));

            WriteBase64InlineIfPresent();
            ThrowIfElementIsXOPInclude(prefix, localName.Value, ns == null ? null : ns.Value);
            Writer.WriteStartElement(prefix, localName, ns);
            _depth++;
        }

        private void ThrowIfElementIsXOPInclude(string prefix, string localName, string ns)
        {
            if (ns == null)
            {
                // This check isn't as thorough as on .NET Framework due to use of internal api's.
                // The scenario that this won't catch is when the XopIncludeNamespace has been added
                // multiple times with different prefixes. I don't believe that will be the case as
                // any attempt to add it with a different prefix would hit this code path anyway.
                var xopPrefix = Writer.LookupPrefix(MtomGlobals.XopIncludeNamespace);
                if (xopPrefix != null && xopPrefix == prefix)
                    ns = MtomGlobals.XopIncludeNamespace;
            }

            if (localName == MtomGlobals.XopIncludeLocalName && ns == MtomGlobals.XopIncludeNamespace)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.MtomDataMustNotContainXopInclude, MtomGlobals.XopIncludeLocalName, MtomGlobals.XopIncludeNamespace)));
        }

        public override void WriteEndElement()
        {
            WriteXOPInclude();
            Writer.WriteEndElement();
            _depth--;
            WriteXOPBinaryParts();
        }

        public override async Task WriteEndElementAsync()
        {
            await WriteXOPIncludeAsync();
            await Writer.WriteEndElementAsync();
            _depth--;
            await WriteXOPBinaryPartsAsync();
        }

        public override void WriteFullEndElement()
        {
            WriteXOPInclude();
            Writer.WriteFullEndElement();
            _depth--;
            WriteXOPBinaryParts();
        }

        public override void WriteValue(IStreamProvider value)
        {
            if (value == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException("value"));

            if (Writer.WriteState == WriteState.Element)
            {
                if (_binaryDataChunks == null)
                {
                    _binaryDataChunks = new List<MtomBinaryData>();
                    _contentID = GenerateUriForMimePart((_mimeParts == null) ? 1 : _mimeParts.Count + 1);
                }
                _binaryDataChunks.Add(new MtomBinaryData(value));
            }
            else
                Writer.WriteValue(value);
        }

        public override void WriteBase64(byte[] buffer, int index, int count)
        {
            if (Writer.WriteState == WriteState.Element)
            {
                if (buffer == null)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException("buffer"));

                // Not checking upper bound because it will be caught by "count".  This is what XmlTextWriter does.
                if (index < 0)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(index), SR.ValueMustBeNonNegative));

                if (count < 0)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(count), SR.ValueMustBeNonNegative));
                if (count > buffer.Length - index)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(count), SR.Format(SR.SizeExceedsRemainingBufferSpace, buffer.Length - index)));

                if (_binaryDataChunks == null)
                {
                    _binaryDataChunks = new List<MtomBinaryData>();
                    _contentID = GenerateUriForMimePart((_mimeParts == null) ? 1 : _mimeParts.Count + 1);
                }

                int totalSize = ValidateSizeOfMessage(_maxSizeInBytes, 0, _totalSizeOfMimeParts);
                totalSize += ValidateSizeOfMessage(_maxSizeInBytes, totalSize, _sizeOfBufferedBinaryData);
                totalSize += ValidateSizeOfMessage(_maxSizeInBytes, totalSize, count);
                _sizeOfBufferedBinaryData += count;
                _binaryDataChunks.Add(new MtomBinaryData(buffer, index, count));
            }
            else
                Writer.WriteBase64(buffer, index, count);
        }

        internal static int ValidateSizeOfMessage(int maxSize, int offset, int size)
        {
            if (size > (maxSize - offset))
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.MtomExceededMaxSizeInBytes, maxSize)));
            return size;
        }

        private void WriteBase64InlineIfPresent()
        {
            if (_binaryDataChunks != null)
            {
                WriteBase64Inline();
            }
        }

        private Task WriteBase64InlineIfPresentAsync()
        {
            if (_binaryDataChunks != null)
            {
                return WriteBase64InlineAsync();
            }
            else
            {
                return Task.CompletedTask;
            }
        }

        private void WriteBase64Inline()
        {
            foreach (MtomBinaryData data in _binaryDataChunks)
            {
                if (data.type == MtomBinaryDataType.Provider)
                {
                    Writer.WriteValue(data.provider);
                }
                else
                {
                    Writer.WriteBase64(data.chunk, 0, data.chunk.Length);
                }
            }
            _sizeOfBufferedBinaryData = 0;
            _binaryDataChunks = null;
            _contentType = null;
            _contentID = null;
        }

        private async Task WriteBase64InlineAsync()
        {
            foreach (MtomBinaryData data in _binaryDataChunks)
            {
                if (data.type == MtomBinaryDataType.Provider)
                {
                    await Writer.WriteValueAsync(data.provider);
                }
                else
                {
                    await Writer.WriteBase64Async(data.chunk, 0, data.chunk.Length);
                }
            }
            _sizeOfBufferedBinaryData = 0;
            _binaryDataChunks = null;
            _contentType = null;
            _contentID = null;
        }

        private void WriteXOPInclude()
        {
            if (_binaryDataChunks == null)
                return;

            bool inline = true;
            long size = 0;
            foreach (MtomBinaryData data in _binaryDataChunks)
            {
                long len = data.Length;
                if (len < 0 || len > (MaxInlinedBytes - size))
                {
                    inline = false;
                    break;
                }
                size += len;
            }

            if (inline)
                WriteBase64Inline();
            else
            {
                if (_mimeParts == null)
                    _mimeParts = new List<MimePart>();

                MimePart mimePart = new MimePart(_binaryDataChunks, _contentID, _contentType, MimeGlobals.EncodingBinary, _sizeOfBufferedBinaryData, _maxSizeInBytes);
                _mimeParts.Add(mimePart);

                _totalSizeOfMimeParts += ValidateSizeOfMessage(_maxSizeInBytes, _totalSizeOfMimeParts, mimePart.sizeInBytes);
                _totalSizeOfMimeParts += ValidateSizeOfMessage(_maxSizeInBytes, _totalSizeOfMimeParts, _mimeWriter.GetBoundarySize());

                Writer.WriteStartElement(MtomGlobals.XopIncludePrefix, MtomGlobals.XopIncludeLocalName, MtomGlobals.XopIncludeNamespace);
                Writer.WriteStartAttribute(MtomGlobals.XopIncludeHrefLocalName, MtomGlobals.XopIncludeHrefNamespace);
                Writer.WriteValue(string.Format(CultureInfo.InvariantCulture, "{0}{1}", MimeGlobals.ContentIDScheme, _contentID));
                Writer.WriteEndAttribute();
                Writer.WriteEndElement();
                _binaryDataChunks = null;
                _sizeOfBufferedBinaryData = 0;
                _contentType = null;
                _contentID = null;
            }
        }

        private async Task WriteXOPIncludeAsync()
        {
            if (_binaryDataChunks == null)
                return;

            bool inline = true;
            long size = 0;
            foreach (MtomBinaryData data in _binaryDataChunks)
            {
                long len = data.Length;
                if (len < 0 || len > (MaxInlinedBytes - size))
                {
                    inline = false;
                    break;
                }
                size += len;
            }

            if (inline)
                await WriteBase64InlineAsync();
            else
            {
                if (_mimeParts == null)
                    _mimeParts = new List<MimePart>();

                MimePart mimePart = new MimePart(_binaryDataChunks, _contentID, _contentType, MimeGlobals.EncodingBinary, _sizeOfBufferedBinaryData, _maxSizeInBytes);
                _mimeParts.Add(mimePart);

                _totalSizeOfMimeParts += ValidateSizeOfMessage(_maxSizeInBytes, _totalSizeOfMimeParts, mimePart.sizeInBytes);
                _totalSizeOfMimeParts += ValidateSizeOfMessage(_maxSizeInBytes, _totalSizeOfMimeParts, _mimeWriter.GetBoundarySize());

                await Writer.WriteStartElementAsync(MtomGlobals.XopIncludePrefix, MtomGlobals.XopIncludeLocalName, MtomGlobals.XopIncludeNamespace);
                Writer.WriteStartAttribute(MtomGlobals.XopIncludeHrefLocalName, MtomGlobals.XopIncludeHrefNamespace);
                Writer.WriteString(string.Format(CultureInfo.InvariantCulture, "{0}{1}", MimeGlobals.ContentIDScheme, _contentID));
                Writer.WriteEndAttribute();
                await Writer.WriteEndElementAsync();
                _binaryDataChunks = null;
                _sizeOfBufferedBinaryData = 0;
                _contentType = null;
                _contentID = null;
            }
        }

        public static string GenerateUriForMimePart(int index)
        {
            return string.Format(CultureInfo.InvariantCulture, "http://tempuri.org/{0}/{1}", index, DateTime.Now.Ticks);
        }

        private void WriteXOPBinaryParts()
        {
            if (_depth > 0 || _mimeWriter.WriteState == MimeWriterState.Closed)
                return;

            if (Writer.WriteState != WriteState.Closed)
                Writer.Flush();

            if (_mimeParts != null)
            {
                foreach (MimePart part in _mimeParts)
                {
                    WriteMimeHeaders(part.contentID, part.contentType, part.contentTransferEncoding);
                    Stream s = _mimeWriter.GetContentStream();
                    int blockSize = 256;
                    int bytesRead = 0;
                    byte[] block = new byte[blockSize];
                    Stream stream = null;
                    foreach (MtomBinaryData data in part.binaryData)
                    {
                        if (data.type == MtomBinaryDataType.Provider)
                        {
                            stream = data.provider.GetStream();
                            if (stream == null)
                                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.XmlInvalidStream));
                            while (true)
                            {
                                bytesRead = stream.Read(block, 0, blockSize);
                                if (bytesRead > 0)
                                    s.Write(block, 0, bytesRead);
                                else
                                    break;
                                if (blockSize < 65536 && bytesRead == blockSize)
                                {
                                    blockSize = blockSize * 16;
                                    block = new byte[blockSize];
                                }
                            }

                            data.provider.ReleaseStream(stream);
                        }
                        else
                        {
                            s.Write(data.chunk, 0, data.chunk.Length);
                        }
                    }
                }
                _mimeParts.Clear();
            }
            _mimeWriter.Close();
        }

        private async Task WriteXOPBinaryPartsAsync()
        {
            if (_depth > 0 || _mimeWriter.WriteState == MimeWriterState.Closed)
                return;

            if (Writer.WriteState != WriteState.Closed)
                await Writer.FlushAsync();

            if (_mimeParts != null)
            {
                foreach (MimePart part in _mimeParts)
                {
                    await WriteMimeHeadersAsync(part.contentID, part.contentType, part.contentTransferEncoding);
                    Stream s = await _mimeWriter.GetContentStreamAsync();
                    int blockSize = 256;
                    int bytesRead = 0;
                    byte[] block = new byte[blockSize];
                    Stream stream = null;
                    foreach (MtomBinaryData data in part.binaryData)
                    {
                        if (data.type == MtomBinaryDataType.Provider)
                        {
                            stream = data.provider.GetStream();
                            if (stream == null)
                                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.XmlInvalidStream));
                            while (true)
                            {
                                bytesRead = stream.Read(block, 0, blockSize);
                                if (bytesRead > 0)
                                    s.Write(block, 0, bytesRead);
                                else
                                    break;
                                if (blockSize < 65536 && bytesRead == blockSize)
                                {
                                    blockSize = blockSize * 16;
                                    block = new byte[blockSize];
                                }
                            }

                            data.provider.ReleaseStream(stream);
                        }
                        else
                        {
                            s.Write(data.chunk, 0, data.chunk.Length);
                        }
                    }
                }
                _mimeParts.Clear();
            }
            await _mimeWriter.CloseAsync();
        }

        private void WriteMimeHeaders(string contentID, string contentType, string contentTransferEncoding)
        {
            _mimeWriter.StartPart();
            if (contentID != null)
                _mimeWriter.WriteHeader(MimeGlobals.ContentIDHeader, string.Format(CultureInfo.InvariantCulture, "<{0}>", contentID));
            if (contentTransferEncoding != null)
                _mimeWriter.WriteHeader(MimeGlobals.ContentTransferEncodingHeader, contentTransferEncoding);
            if (contentType != null)
                _mimeWriter.WriteHeader(MimeGlobals.ContentTypeHeader, contentType);
        }

        private async Task WriteMimeHeadersAsync(string contentID, string contentType, string contentTransferEncoding)
        {
            await _mimeWriter.StartPartAsync();
            if (contentID != null)
                _mimeWriter.WriteHeader(MimeGlobals.ContentIDHeader, string.Format(CultureInfo.InvariantCulture, "<{0}>", contentID));
            if (contentTransferEncoding != null)
                _mimeWriter.WriteHeader(MimeGlobals.ContentTransferEncodingHeader, contentTransferEncoding);
            if (contentType != null)
                _mimeWriter.WriteHeader(MimeGlobals.ContentTypeHeader, contentType);
        }

        public override void Close()
        {
            if (!_isClosed)
            {
                _isClosed = true;
                if (IsInitialized)
                {
                    WriteXOPInclude();
                    if (Writer.WriteState == WriteState.Element ||
                        Writer.WriteState == WriteState.Attribute ||
                        Writer.WriteState == WriteState.Content)
                    {
                        Writer.WriteEndDocument();
                    }
                    Writer.Flush();
                    _depth = 0;
                    WriteXOPBinaryParts();
                    Writer.Close();
                }
            }
        }

        private void CheckIfStartContentTypeAttribute(string localName, string ns)
        {
            if (localName != null && localName == MtomGlobals.MimeContentTypeLocalName
                && ns != null && (ns == MtomGlobals.MimeContentTypeNamespace200406 || ns == MtomGlobals.MimeContentTypeNamespace200505))
            {
                _contentTypeStream = new MemoryStream();
                this._infosetWriter = Writer;
                _writer = XmlDictionaryWriter.CreateBinaryWriter(_contentTypeStream);
                Writer.WriteStartElement("Wrapper");
                Writer.WriteStartAttribute(localName, ns);
            }
        }

        private void CheckIfEndContentTypeAttribute()
        {
            if (_contentTypeStream != null)
            {
                Writer.WriteEndAttribute();
                Writer.WriteEndElement();
                Writer.Flush();
                _contentTypeStream.Position = 0;
                XmlReader contentTypeReader = XmlDictionaryReader.CreateBinaryReader(_contentTypeStream, null, XmlDictionaryReaderQuotas.Max, null, null);
                while (contentTypeReader.Read())
                {
                    if (contentTypeReader.IsStartElement("Wrapper"))
                    {
                        _contentType = contentTypeReader.GetAttribute(MtomGlobals.MimeContentTypeLocalName, MtomGlobals.MimeContentTypeNamespace200406);
                        if (_contentType == null)
                        {
                            _contentType = contentTypeReader.GetAttribute(MtomGlobals.MimeContentTypeLocalName, MtomGlobals.MimeContentTypeNamespace200505);
                        }
                        break;
                    }
                }
                _writer = _infosetWriter;
                this._infosetWriter = null;
                _contentTypeStream = null;
                if (_contentType != null)
                    Writer.WriteString(_contentType);
            }
        }

        public override void Flush()
        {
            if (IsInitialized)
                Writer.Flush();
        }

        public override Task FlushAsync()
        {
            if (IsInitialized)
                return Writer.FlushAsync();
            else
                return Task.CompletedTask;
        }

        public override string LookupPrefix(string ns)
        {
            return Writer.LookupPrefix(ns);
        }

        public override XmlWriterSettings Settings
        {
            get
            {
                return Writer.Settings;
            }
        }

        public override void WriteAttributes(XmlReader reader, bool defattr)
        {
            Writer.WriteAttributes(reader, defattr);
        }

        public override void WriteBinHex(byte[] buffer, int index, int count)
        {
            WriteBase64InlineIfPresent();
            Writer.WriteBinHex(buffer, index, count);
        }

        public override void WriteCData(string text)
        {
            WriteBase64InlineIfPresent();
            Writer.WriteCData(text);
        }

        public override void WriteCharEntity(char ch)
        {
            WriteBase64InlineIfPresent();
            Writer.WriteCharEntity(ch);
        }

        public override void WriteChars(char[] buffer, int index, int count)
        {
            WriteBase64InlineIfPresent();
            Writer.WriteChars(buffer, index, count);
        }

        public override void WriteComment(string text)
        {
            // Don't write comments after the document element
            if (_depth == 0 && _mimeWriter.WriteState == MimeWriterState.Closed)
                return;

            WriteBase64InlineIfPresent();
            Writer.WriteComment(text);
        }

        public override void WriteDocType(string name, string pubid, string sysid, string subset)
        {
            WriteBase64InlineIfPresent();
            Writer.WriteDocType(name, pubid, sysid, subset);
        }
#if NO
        public override void WriteElementSubset(ArraySegment<byte> buffer)
        {
            Writer.WriteElementSubset(buffer);
        }
#endif
        public override void WriteEndAttribute()
        {
            CheckIfEndContentTypeAttribute();
            Writer.WriteEndAttribute();
        }

        public override void WriteEndDocument()
        {
            WriteXOPInclude();
            Writer.WriteEndDocument();
            _depth = 0;
            WriteXOPBinaryParts();
        }

        public override void WriteEntityRef(string name)
        {
            WriteBase64InlineIfPresent();
            Writer.WriteEntityRef(name);
        }

        public override void WriteName(string name)
        {
            WriteBase64InlineIfPresent();
            Writer.WriteName(name);
        }

        public override void WriteNmToken(string name)
        {
            WriteBase64InlineIfPresent();
            Writer.WriteNmToken(name);
        }

        protected override void WriteTextNode(XmlDictionaryReader reader, bool attribute)
        {
            Type type = reader.ValueType;
            if (type == typeof(string))
            {
                if (reader.CanReadValueChunk)
                {
                    if (_chars == null)
                    {
                        _chars = new char[256];
                    }
                    int count;
                    while ((count = reader.ReadValueChunk(_chars, 0, _chars.Length)) > 0)
                    {
                        WriteChars(_chars, 0, count);
                    }
                }
                else
                {
                    WriteString(reader.Value);
                }
                if (!attribute)
                {
                    reader.Read();
                }
            }
            else if (type == typeof(byte[]))
            {
                if (reader.CanReadBinaryContent)
                {
                    // Its best to read in buffers that are a multiple of 3 so we don't break base64 boundaries when converting text
                    if (_bytes == null)
                    {
                        _bytes = new byte[384];
                    }
                    int count;
                    while ((count = reader.ReadValueAsBase64(_bytes, 0, _bytes.Length)) > 0)
                    {
                        WriteBase64(_bytes, 0, count);
                    }
                }
                else
                {
                    WriteString(reader.Value);
                }
                if (!attribute)
                {
                    reader.Read();
                }
            }
            else
            {
                base.WriteTextNode(reader, attribute);
            }
        }

        public override void WriteNode(XPathNavigator navigator, bool defattr)
        {
            WriteBase64InlineIfPresent();
            Writer.WriteNode(navigator, defattr);
        }

        public override void WriteProcessingInstruction(string name, string text)
        {
            WriteBase64InlineIfPresent();
            Writer.WriteProcessingInstruction(name, text);
        }

        public override void WriteQualifiedName(string localName, string namespaceUri)
        {
            WriteBase64InlineIfPresent();
            Writer.WriteQualifiedName(localName, namespaceUri);
        }

        public override void WriteRaw(char[] buffer, int index, int count)
        {
            WriteBase64InlineIfPresent();
            Writer.WriteRaw(buffer, index, count);
        }

        public override void WriteRaw(string data)
        {
            WriteBase64InlineIfPresent();
            Writer.WriteRaw(data);
        }

        public override void WriteStartAttribute(string prefix, string localName, string ns)
        {
            Writer.WriteStartAttribute(prefix, localName, ns);
            CheckIfStartContentTypeAttribute(localName, ns);
        }

        public override void WriteStartAttribute(string prefix, XmlDictionaryString localName, XmlDictionaryString ns)
        {
            Writer.WriteStartAttribute(prefix, localName, ns);
            if (localName != null && ns != null)
                CheckIfStartContentTypeAttribute(localName.Value, ns.Value);
        }

        public override void WriteStartDocument()
        {
            Writer.WriteStartDocument();
        }

        public override void WriteStartDocument(bool standalone)
        {
            Writer.WriteStartDocument(standalone);
        }

        public override WriteState WriteState
        {
            get
            {
                return Writer.WriteState;
            }
        }

        public override void WriteString(string text)
        {
            // Don't write whitespace after the document element
            if (_depth == 0 && _mimeWriter.WriteState == MimeWriterState.Closed && XmlConverter.IsWhitespace(text))
                return;

            WriteBase64InlineIfPresent();
            Writer.WriteString(text);
        }

        public override void WriteString(XmlDictionaryString value)
        {
            // Don't write whitespace after the document element
            if (_depth == 0 && _mimeWriter.WriteState == MimeWriterState.Closed && XmlConverter.IsWhitespace(value.Value))
                return;

            WriteBase64InlineIfPresent();
            Writer.WriteString(value);
        }

        public override void WriteSurrogateCharEntity(char lowChar, char highChar)
        {
            WriteBase64InlineIfPresent();
            Writer.WriteSurrogateCharEntity(lowChar, highChar);
        }

        public override void WriteWhitespace(string whitespace)
        {
            // Don't write whitespace after the document element
            if (_depth == 0 && _mimeWriter.WriteState == MimeWriterState.Closed)
                return;

            WriteBase64InlineIfPresent();
            Writer.WriteWhitespace(whitespace);
        }

        public override void WriteValue(object value)
        {
            IStreamProvider sp = value as IStreamProvider;
            if (sp != null)
            {
                WriteValue(sp);
            }
            else
            {
                WriteBase64InlineIfPresent();
                Writer.WriteValue(value);
            }
        }

        public override void WriteValue(string value)
        {
            // Don't write whitespace after the document element
            if (_depth == 0 && _mimeWriter.WriteState == MimeWriterState.Closed && XmlConverter.IsWhitespace(value))
                return;

            WriteBase64InlineIfPresent();
            Writer.WriteValue(value);
        }

        public override void WriteValue(bool value)
        {
            WriteBase64InlineIfPresent();
            Writer.WriteValue(value);
        }

        public override void WriteValue(DateTime value)
        {
            WriteBase64InlineIfPresent();
            Writer.WriteValue(value);
        }

        public override void WriteValue(double value)
        {
            WriteBase64InlineIfPresent();
            Writer.WriteValue(value);
        }

        public override void WriteValue(int value)
        {
            WriteBase64InlineIfPresent();
            Writer.WriteValue(value);
        }

        public override void WriteValue(long value)
        {
            WriteBase64InlineIfPresent();
            Writer.WriteValue(value);
        }

        public override void WriteValue(XmlDictionaryString value)
        {
            // Don't write whitespace after the document element
            if (_depth == 0 && _mimeWriter.WriteState == MimeWriterState.Closed && XmlConverter.IsWhitespace(value.Value))
                return;

            WriteBase64InlineIfPresent();
            Writer.WriteValue(value);
        }

        public override void WriteXmlnsAttribute(string prefix, string ns)
        {
            Writer.WriteXmlnsAttribute(prefix, ns);
        }

        public override void WriteXmlnsAttribute(string prefix, XmlDictionaryString ns)
        {
            Writer.WriteXmlnsAttribute(prefix, ns);
        }

        public override string XmlLang
        {
            get
            {
                return Writer.XmlLang;
            }
        }

        public override XmlSpace XmlSpace
        {
            get
            {
                return Writer.XmlSpace;
            }
        }

        private static class MimeBoundaryGenerator
        {
            private static long id;
            private static string prefix;

            static MimeBoundaryGenerator()
            {
                prefix = string.Concat(Guid.NewGuid().ToString(), "+id=");
            }

            internal static string Next()
            {
                long nextId = Interlocked.Increment(ref id);
                return string.Format(CultureInfo.InvariantCulture, "{0}{1}", prefix, nextId);
            }
        }

        private class MimePart
        {
            internal IList<MtomBinaryData> binaryData;
            internal string contentID;
            internal string contentType;
            internal string contentTransferEncoding;
            internal int sizeInBytes;

            internal MimePart(IList<MtomBinaryData> binaryData, string contentID, string contentType, string contentTransferEncoding, int sizeOfBufferedBinaryData, int maxSizeInBytes)
            {
                this.binaryData = binaryData;
                this.contentID = contentID;
                this.contentType = contentType ?? MtomGlobals.DefaultContentTypeForBinary;
                this.contentTransferEncoding = contentTransferEncoding;
                sizeInBytes = GetSize(contentID, contentType, contentTransferEncoding, sizeOfBufferedBinaryData, maxSizeInBytes);
            }

            private static int GetSize(string contentID, string contentType, string contentTransferEncoding, int sizeOfBufferedBinaryData, int maxSizeInBytes)
            {
                int size = XmlMtomWriter.ValidateSizeOfMessage(maxSizeInBytes, 0, MimeGlobals.CRLF.Length * 3);
                if (contentTransferEncoding != null)
                    size += XmlMtomWriter.ValidateSizeOfMessage(maxSizeInBytes, size, MimeWriter.GetHeaderSize(MimeGlobals.ContentTransferEncodingHeader, contentTransferEncoding, maxSizeInBytes));
                if (contentType != null)
                    size += XmlMtomWriter.ValidateSizeOfMessage(maxSizeInBytes, size, MimeWriter.GetHeaderSize(MimeGlobals.ContentTypeHeader, contentType, maxSizeInBytes));
                if (contentID != null)
                {
                    size += XmlMtomWriter.ValidateSizeOfMessage(maxSizeInBytes, size, MimeWriter.GetHeaderSize(MimeGlobals.ContentIDHeader, contentID, maxSizeInBytes));
                    size += XmlMtomWriter.ValidateSizeOfMessage(maxSizeInBytes, size, 2); // include '<' and '>'
                }
                size += XmlMtomWriter.ValidateSizeOfMessage(maxSizeInBytes, size, sizeOfBufferedBinaryData);
                return size;
            }
        }
    }


    internal static class MtomGlobals
    {
        internal static string XopIncludeLocalName = "Include";
        internal static string XopIncludeNamespace = "http://www.w3.org/2004/08/xop/include";
        internal static string XopIncludePrefix = "xop";
        internal static string XopIncludeHrefLocalName = "href";
        internal static string XopIncludeHrefNamespace = string.Empty;
        internal static string MediaType = "multipart";
        internal static string MediaSubtype = "related";
        internal static string BoundaryParam = "boundary";
        internal static string TypeParam = "type";
        internal static string XopMediaType = "application";
        internal static string XopMediaSubtype = "xop+xml";
        internal static string XopType = "application/xop+xml";
        internal static string StartParam = "start";
        internal static string StartInfoParam = "start-info";
        internal static string ActionParam = "action";
        internal static string CharsetParam = "charset";
        internal static string MimeContentTypeLocalName = "contentType";
        internal static string MimeContentTypeNamespace200406 = "http://www.w3.org/2004/06/xmlmime";
        internal static string MimeContentTypeNamespace200505 = "http://www.w3.org/2005/05/xmlmime";
        internal static string DefaultContentTypeForBinary = "application/octet-stream";
    }

    internal static class MimeGlobals
    {
        internal static string MimeVersionHeader = "MIME-Version";
        internal static string DefaultVersion = "1.0";
        internal static string ContentIDScheme = "cid:";
        internal static string ContentIDHeader = "Content-ID";
        internal static string ContentTypeHeader = "Content-Type";
        internal static string ContentTransferEncodingHeader = "Content-Transfer-Encoding";
        internal static string EncodingBinary = "binary";
        internal static string Encoding8bit = "8bit";
        internal static byte[] COLONSPACE = new byte[] { (byte)':', (byte)' ' };
        internal static byte[] DASHDASH = new byte[] { (byte)'-', (byte)'-' };
        internal static byte[] CRLF = new byte[] { (byte)'\r', (byte)'\n' };
        // Per RFC2045, preceding CRLF sequence is part of the boundary. MIME boundary tags begin with --
        internal static byte[] BoundaryPrefix = new byte[] { (byte)'\r', (byte)'\n', (byte)'-', (byte)'-' };
    }

    internal enum MimeWriterState
    {
        Start,
        StartPreface,
        StartPart,
        Header,
        Content,
        Closed,
    }

    internal class MimeWriter
    {
        private Stream stream;
        private byte[] boundaryBytes;
        private MimeWriterState state;
        private BufferedWrite bufferedWrite;
        private Stream contentStream;

        internal MimeWriter(Stream stream, string boundary)
        {
            if (stream == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(stream));
            if (boundary == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(boundary));

            this.stream = stream;
            boundaryBytes = MimeWriter.GetBoundaryBytes(boundary);
            state = MimeWriterState.Start;
            bufferedWrite = new BufferedWrite();
        }

        internal static int GetHeaderSize(string name, string value, int maxSizeInBytes)
        {
            if (name == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(name));
            if (value == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));

            int size = XmlMtomWriter.ValidateSizeOfMessage(maxSizeInBytes, 0, MimeGlobals.COLONSPACE.Length + MimeGlobals.CRLF.Length);
            size += XmlMtomWriter.ValidateSizeOfMessage(maxSizeInBytes, size, name.Length);
            size += XmlMtomWriter.ValidateSizeOfMessage(maxSizeInBytes, size, value.Length);
            return size;
        }

        internal static byte[] GetBoundaryBytes(string boundary)
        {
            byte[] boundaryBytes = new byte[boundary.Length + MimeGlobals.BoundaryPrefix.Length];
            for (int i = 0; i < MimeGlobals.BoundaryPrefix.Length; i++)
                boundaryBytes[i] = MimeGlobals.BoundaryPrefix[i];
            Encoding.ASCII.GetBytes(boundary, 0, boundary.Length, boundaryBytes, MimeGlobals.BoundaryPrefix.Length);
            return boundaryBytes;
        }

        internal MimeWriterState WriteState
        {
            get
            {
                return state;
            }
        }

        internal int GetBoundarySize()
        {
            return boundaryBytes.Length;
        }

        internal void StartPreface()
        {
            if (state != MimeWriterState.Start)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.MimeWriterInvalidStateForStartPreface, state.ToString())));

            state = MimeWriterState.StartPreface;
        }

        internal async Task StartPartAsync()
        {
            switch (state)
            {
                case MimeWriterState.StartPart:
                case MimeWriterState.Closed:
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.MimeWriterInvalidStateForStartPart, state.ToString())));
                default:
                    break;
            }

            state = MimeWriterState.StartPart;

            if (contentStream != null)
            {
                await contentStream.FlushAsync();
                contentStream = null;
            }

            bufferedWrite.Write(boundaryBytes);
            bufferedWrite.Write(MimeGlobals.CRLF);
        }

        internal void StartPart()
        {
            switch (state)
            {
                case MimeWriterState.StartPart:
                case MimeWriterState.Closed:
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.MimeWriterInvalidStateForStartPart, state.ToString())));
                default:
                    break;
            }

            state = MimeWriterState.StartPart;

            if (contentStream != null)
            {
                contentStream.Flush();
                contentStream = null;
            }

            bufferedWrite.Write(boundaryBytes);
            bufferedWrite.Write(MimeGlobals.CRLF);
        }

        internal void Close()
        {
            switch (state)
            {
                case MimeWriterState.Closed:
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.MimeWriterInvalidStateForClose, state.ToString())));
                default:
                    break;
            }

            state = MimeWriterState.Closed;

            if (contentStream != null)
            {
                contentStream.Flush();
                contentStream = null;
            }

            bufferedWrite.Write(boundaryBytes);
            bufferedWrite.Write(MimeGlobals.DASHDASH);
            bufferedWrite.Write(MimeGlobals.CRLF);

            Flush();
        }

        internal async Task CloseAsync()
        {
            switch (state)
            {
                case MimeWriterState.Closed:
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.MimeWriterInvalidStateForClose, state.ToString())));
                default:
                    break;
            }

            state = MimeWriterState.Closed;

            if (contentStream != null)
            {
                await contentStream.FlushAsync();
                contentStream = null;
            }

            bufferedWrite.Write(boundaryBytes);
            bufferedWrite.Write(MimeGlobals.DASHDASH);
            bufferedWrite.Write(MimeGlobals.CRLF);

            await FlushAsync();
        }

        private async Task FlushAsync()
        {
            if (bufferedWrite.Length > 0)
            {
                await stream.WriteAsync(bufferedWrite.GetBuffer(), 0, bufferedWrite.Length);
                bufferedWrite.Reset();
            }
        }

        private void Flush()
        {
            if (bufferedWrite.Length > 0)
            {
                stream.Write(bufferedWrite.GetBuffer(), 0, bufferedWrite.Length);
                bufferedWrite.Reset();
            }
        }

        internal void WriteHeader(string name, string value)
        {
            if (name == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(name));
            if (value == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(value));

            switch (state)
            {
                case MimeWriterState.Start:
                case MimeWriterState.Content:
                case MimeWriterState.Closed:
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.MimeWriterInvalidStateForHeader, state.ToString())));
                default:
                    break;
            }

            state = MimeWriterState.Header;

            bufferedWrite.Write(name);
            bufferedWrite.Write(MimeGlobals.COLONSPACE);
            bufferedWrite.Write(value);
            bufferedWrite.Write(MimeGlobals.CRLF);
        }

        internal async Task<Stream> GetContentStreamAsync()
        {
            switch (state)
            {
                case MimeWriterState.Start:
                case MimeWriterState.Content:
                case MimeWriterState.Closed:
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.MimeWriterInvalidStateForContent, state.ToString())));
                default:
                    break;
            }

            state = MimeWriterState.Content;
            bufferedWrite.Write(MimeGlobals.CRLF);
            await FlushAsync();
            contentStream = stream;
            return contentStream;
        }

        internal Stream GetContentStream()
        {
            switch (state)
            {
                case MimeWriterState.Start:
                case MimeWriterState.Content:
                case MimeWriterState.Closed:
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.MimeWriterInvalidStateForContent, state.ToString())));
                default:
                    break;
            }

            state = MimeWriterState.Content;
            bufferedWrite.Write(MimeGlobals.CRLF);
            Flush();
            contentStream = stream;
            return contentStream;
        }
    }

    internal class BufferedWrite
    {
        private byte[] buffer;
        private int offset;

        internal BufferedWrite()
            : this(256)
        {
        }

        internal BufferedWrite(int initialSize)
        {
            buffer = new byte[initialSize];
        }

        private void EnsureBuffer(int count)
        {
            int currSize = buffer.Length;
            if (count > currSize - offset)
            {
                int newSize = currSize;
                do
                {
                    if (newSize == int.MaxValue)
                        throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.WriteBufferOverflow));
                    newSize = (newSize < int.MaxValue / 2) ? newSize * 2 : int.MaxValue;
                }
                while (count > newSize - offset);
                byte[] newBuffer = new byte[newSize];
                Buffer.BlockCopy(buffer, 0, newBuffer, 0, offset);
                buffer = newBuffer;
            }
        }

        internal int Length
        {
            get
            {
                return offset;
            }
        }

        internal byte[] GetBuffer()
        {
            return buffer;
        }

        internal void Reset()
        {
            offset = 0;
        }

        internal void Write(byte[] value)
        {
            Write(value, 0, value.Length);
        }

        internal void Write(byte[] value, int index, int count)
        {
            EnsureBuffer(count);
            Buffer.BlockCopy(value, index, buffer, offset, count);
            offset += count;
        }

        internal void Write(string value)
        {
            Write(value, 0, value.Length);
        }

        internal void Write(string value, int index, int count)
        {
            EnsureBuffer(count);
            for (int i = 0; i < count; i++)
            {
                char c = value[index + i];
                if ((ushort)c > 0xFF)
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new FormatException(SR.Format(SR.MimeHeaderInvalidCharacter, c, ((int)c).ToString("X", CultureInfo.InvariantCulture))));
                buffer[offset + i] = (byte)c;
            }
            offset += count;
        }
    }

    internal enum MtomBinaryDataType { Provider, Segment }

    internal class MtomBinaryData
    {
        internal MtomBinaryDataType type;

        internal IStreamProvider provider;
        internal byte[] chunk;

        internal MtomBinaryData(IStreamProvider provider)
        {
            type = MtomBinaryDataType.Provider;
            this.provider = provider;
        }

        internal MtomBinaryData(byte[] buffer, int offset, int count)
        {
            type = MtomBinaryDataType.Segment;
            chunk = new byte[count];
            Buffer.BlockCopy(buffer, offset, chunk, 0, count);
        }

        internal long Length
        {
            get
            {
                if (type == MtomBinaryDataType.Segment)
                    return chunk.Length;

                return -1;
            }
        }
    }
}
