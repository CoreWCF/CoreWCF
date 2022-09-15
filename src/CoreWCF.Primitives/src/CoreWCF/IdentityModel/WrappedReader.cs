// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Text;
using System.Xml;
using HexBinary = CoreWCF.Security.SoapHexBinary;

namespace CoreWCF.IdentityModel
{
    internal sealed class WrappedReader : DelegatingXmlDictionaryReader, IXmlLineInfo
    {
        private MemoryStream _contentStream;
        private TextReader _contentReader;
        private bool _recordDone;
        private int _depth;
        private bool _disposed;

        public WrappedReader(XmlDictionaryReader reader)
        {
            if (reader == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(reader));
            }
            if (!reader.IsStartElement())
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.InnerReaderMustBeAtElement)));
            }
            XmlTokens = new XmlTokenStream(32);
            base.InitializeInnerReader(reader);
            Record();
        }

        public int LineNumber
        {
            get
            {
                IXmlLineInfo lineInfo = base.InnerReader as IXmlLineInfo;
                if (lineInfo == null)
                {
                    return 1;
                }
                return lineInfo.LineNumber;
            }
        }

        public int LinePosition
        {
            get
            {
                IXmlLineInfo lineInfo = base.InnerReader as IXmlLineInfo;
                if (lineInfo == null)
                {
                    return 1;
                }
                return lineInfo.LinePosition;
            }
        }

        public XmlTokenStream XmlTokens { get; }

        public override void Close()
        {
            OnEndOfContent();
            base.InnerReader.Close();
        }

        public bool HasLineInfo()
        {
            IXmlLineInfo lineInfo = base.InnerReader as IXmlLineInfo;
            return lineInfo != null && lineInfo.HasLineInfo();
        }

        public override void MoveToAttribute(int index)
        {
            OnEndOfContent();
            base.InnerReader.MoveToAttribute(index);
        }

        public override bool MoveToAttribute(string name)
        {
            OnEndOfContent();
            return base.InnerReader.MoveToAttribute(name);
        }

        public override bool MoveToAttribute(string name, string ns)
        {
            OnEndOfContent();
            return base.InnerReader.MoveToAttribute(name, ns);
        }

        public override bool MoveToElement()
        {
            OnEndOfContent();
            return base.MoveToElement();
        }

        public override bool MoveToFirstAttribute()
        {
            OnEndOfContent();
            return base.MoveToFirstAttribute();
        }

        public override bool MoveToNextAttribute()
        {
            OnEndOfContent();
            return base.MoveToNextAttribute();
        }

        private void OnEndOfContent()
        {
            if (_contentReader != null)
            {
                _contentReader.Close();
                _contentReader = null;
            }
            if (_contentStream != null)
            {
                _contentStream.Close();
                _contentStream = null;
            }
        }

        public override bool Read()
        {
            OnEndOfContent();
            if (!base.Read())
            {
                return false;
            }
            if (!_recordDone)
            {
                Record();
            }
            return true;
        }

        private int ReadBinaryContent(byte[] buffer, int offset, int count, bool isBase64)
        {
            CryptoHelper.ValidateBufferBounds(buffer, offset, count);

            //
            // Concatentate text nodes to get entire element value before attempting to convert
            // XmlDictionaryReader.CreateDictionaryReader( XmlReader ) creates a reader that returns base64 in a single text node
            // XmlDictionaryReader.CreateTextReader( Stream ) creates a reader that produces multiple text and whitespace nodes
            // Attribute nodes consist of only a single value
            //
            if (_contentStream == null)
            {
                string encodedValue;
                if (NodeType == XmlNodeType.Attribute)
                {
                    encodedValue = Value;
                }
                else
                {
                    StringBuilder fullText = new StringBuilder(1000);
                    while (NodeType != XmlNodeType.Element && NodeType != XmlNodeType.EndElement)
                    {
                        switch (NodeType)
                        {
                            // concatenate text nodes
                            case XmlNodeType.Text:
                                fullText.Append(Value);
                                break;

                            // skip whitespace
                            case XmlNodeType.Whitespace:
                                break;
                        }

                        Read();
                    }

                    encodedValue = fullText.ToString();
                }

                byte[] value = isBase64 ? Convert.FromBase64String(encodedValue) : HexBinary.Parse(encodedValue).Value;
                _contentStream = new MemoryStream(value);
            }

            int read = _contentStream.Read(buffer, offset, count);
            if (read == 0)
            {
                _contentStream.Close();
                _contentStream = null;
            }

            return read;
        }

        public override int ReadContentAsBase64(byte[] buffer, int offset, int count)
        {
            return ReadBinaryContent(buffer, offset, count, true);
        }

        public override int ReadContentAsBinHex(byte[] buffer, int offset, int count)
        {
            return ReadBinaryContent(buffer, offset, count, false);
        }

        public override int ReadValueChunk(char[] chars, int offset, int count)
        {
            if (_contentReader == null)
            {
                _contentReader = new StringReader(Value);
            }
            return _contentReader.Read(chars, offset, count);
        }

        private void Record()
        {
            switch (NodeType)
            {
                case XmlNodeType.Element:
                    {
                        bool isEmpty = base.InnerReader.IsEmptyElement;
                        XmlTokens.AddElement(base.InnerReader.Prefix, base.InnerReader.LocalName, base.InnerReader.NamespaceURI, isEmpty);
                        if (base.InnerReader.MoveToFirstAttribute())
                        {
                            do
                            {
                                XmlTokens.AddAttribute(base.InnerReader.Prefix, base.InnerReader.LocalName, base.InnerReader.NamespaceURI, base.InnerReader.Value);
                            }
                            while (base.InnerReader.MoveToNextAttribute());
                            base.InnerReader.MoveToElement();
                        }
                        if (!isEmpty)
                        {
                            _depth++;
                        }
                        else if (_depth == 0)
                        {
                            _recordDone = true;
                        }
                        break;
                    }
                case XmlNodeType.CDATA:
                case XmlNodeType.Comment:
                case XmlNodeType.Text:
                case XmlNodeType.EntityReference:
                case XmlNodeType.EndEntity:
                case XmlNodeType.SignificantWhitespace:
                case XmlNodeType.Whitespace:
                    {
                        XmlTokens.Add(NodeType, Value);
                        break;
                    }
                case XmlNodeType.EndElement:
                    {
                        XmlTokens.Add(NodeType, Value);
                        if (--_depth == 0)
                        {
                            _recordDone = true;
                        }
                        break;
                    }
                case XmlNodeType.DocumentType:
                case XmlNodeType.XmlDeclaration:
                    {
                        break;
                    }
                default:
                    {
                       
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.UnsupportedNodeTypeInReader,
                     base.InnerReader.NodeType, base.InnerReader.Name)));
                            
                    }

            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                //
                // Free all of our managed resources
                //
                if (_contentReader != null)
                {
                    _contentReader.Dispose();
                    _contentReader = null;
                }

                if (_contentStream != null)
                {
                    _contentStream.Dispose();
                    _contentStream = null;
                }
            }

            // Free native resources, if any.

            _disposed = true;
        }
    }

    sealed internal class XmlTokenStream : ISecurityElement
    {
        private int count;
        private XmlTokenEntry[] _entries;
        private string _excludedElement;
        private int? _excludedElementDepth;
        private string _excludedElementNamespace;

        public XmlTokenStream(int initialSize)
        {
            if (initialSize < 1)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(initialSize), SR.Format(SR.ValueMustBeGreaterThanZero)));
            }
            _entries = new XmlTokenEntry[initialSize];
        }
        
        // This constructor is used by the Trim method to reduce the size of the XmlTokenEntry array to the minimum required.
        public XmlTokenStream(XmlTokenStream other)
        {
            count = other.count;
            _excludedElement = other._excludedElement;
            _excludedElementDepth = other._excludedElementDepth;
            _excludedElementNamespace = other._excludedElementNamespace;
            _entries = new XmlTokenEntry[count];
            Array.Copy(other._entries, _entries, count);
        }
        
        public void Add(XmlNodeType type, string value)
        {
            EnsureCapacityToAdd();
            _entries[count++].Set(type, value);
        }

        public void AddAttribute(string prefix, string localName, string namespaceUri, string value)
        {
            EnsureCapacityToAdd();
            _entries[count++].SetAttribute(prefix, localName, namespaceUri, value);
        }

        public void AddElement(string prefix, string localName, string namespaceUri, bool isEmptyElement)
        {
            EnsureCapacityToAdd();
            _entries[count++].SetElement(prefix, localName, namespaceUri, isEmptyElement);
        }

        private void EnsureCapacityToAdd()
        {
            if (count == _entries.Length)
            {
                XmlTokenEntry[] newBuffer = new XmlTokenEntry[_entries.Length * 2];
                Array.Copy(_entries, 0, newBuffer, 0, count);
                _entries = newBuffer;
            }
        }

        public void SetElementExclusion(string excludedElement, string excludedElementNamespace)
        {
            SetElementExclusion(excludedElement, excludedElementNamespace, null);
        }

        public void SetElementExclusion(string excludedElement, string excludedElementNamespace, int? excludedElementDepth)
        {
            _excludedElement = excludedElement;
            _excludedElementDepth = excludedElementDepth;
            _excludedElementNamespace = excludedElementNamespace;
        }

        /// <summary>
        /// Free unneeded entries from array
        /// </summary>
        /// <returns></returns>
        public XmlTokenStream Trim()
        {
            return new XmlTokenStream(this);
        }

        public XmlTokenStreamWriter GetWriter()
        {
            return new XmlTokenStreamWriter( _entries, count, _excludedElement, _excludedElementDepth, _excludedElementNamespace );
        }

        public void WriteTo(XmlDictionaryWriter writer, DictionaryManager dictionaryManager)
        {
            GetWriter().WriteTo(writer, dictionaryManager);
        }

        bool ISecurityElement.HasId
        {
            get { return false; }
        }

        string ISecurityElement.Id
        {
            get { return null; }
        }

        internal class XmlTokenStreamWriter : ISecurityElement
        {
            private XmlTokenEntry[] _entries;
            private int? _excludedElementDepth;

            public XmlTokenStreamWriter(XmlTokenEntry[] entries,
                                         int count,
                                         string excludedElement,
                                         int? excludedElementDepth,
                                         string excludedElementNamespace)
            {
                if (entries == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(entries));
                }
                _entries = entries;
                Count = count;
                ExcludedElement = excludedElement;
                _excludedElementDepth = excludedElementDepth;
                ExcludedElementNamespace = excludedElementNamespace;
            }

            public int Count { get; }

            public int Position { get; private set; }

            public XmlNodeType NodeType
            {
                get { return _entries[Position].nodeType; }
            }

            public bool IsEmptyElement
            {
                get { return _entries[Position].IsEmptyElement; }
            }

            public string Prefix
            {
                get { return _entries[Position].prefix; }
            }

            public string LocalName
            {
                get { return _entries[Position].localName; }
            }

            public string NamespaceUri
            {
                get { return _entries[Position].namespaceUri; }
            }

            public string Value
            {
                get { return _entries[Position].Value; }
            }

            public string ExcludedElement { get; }

            public string ExcludedElementNamespace { get; }
            bool ISecurityElement.HasId
            {
                get { return false; }
            }

            string ISecurityElement.Id
            {
                get { return null; }
            }

            public bool MoveToFirst()
            {
                Position = 0;
                return Count > 0;
            }

            public bool MoveToFirstAttribute()
            {
                DiagnosticUtility.DebugAssert(NodeType == XmlNodeType.Element, "");
                if (Position < Count - 1 && _entries[Position + 1].nodeType == XmlNodeType.Attribute)
                {
                    Position++;
                    return true;
                }
                else
                {
                    return false;
                }
            }

            public bool MoveToNext()
            {
                if (Position < Count - 1)
                {
                    Position++;
                    return true;
                }
                return false;
            }

            public bool MoveToNextAttribute()
            {
                DiagnosticUtility.DebugAssert(NodeType == XmlNodeType.Attribute, "");
                if (Position < Count - 1 && _entries[Position + 1].nodeType == XmlNodeType.Attribute)
                {
                    Position++;
                    return true;
                }
                else
                {
                    return false;
                }
            }

            public void WriteTo(XmlDictionaryWriter writer, DictionaryManager dictionaryManager)
            {
                if (writer == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(writer)));
                }
                if (!MoveToFirst())
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.XmlTokenBufferIsEmpty)));
                }
                int depth = 0;
                int recordedDepth = -1;
                bool include = true;
                do
                {
                    switch (NodeType)
                    {
                        case XmlNodeType.Element:
                            bool isEmpty = IsEmptyElement;
                            depth++;
                            if (include
                                && (null == _excludedElementDepth || _excludedElementDepth == (depth - 1))
                                && LocalName == ExcludedElement 
                                && NamespaceUri == ExcludedElementNamespace)
                            {
                                include = false;
                                recordedDepth = depth;
                            }
                            if (include)
                            {
                                writer.WriteStartElement(Prefix, LocalName, NamespaceUri);
                            }
                            if (MoveToFirstAttribute())
                            {
                                do
                                {
                                    if (include)
                                    {
                                        writer.WriteAttributeString(Prefix, LocalName, NamespaceUri, Value);
                                    }
                                }
                                while (MoveToNextAttribute());
                            }
                            if (isEmpty)
                            {
                                goto case XmlNodeType.EndElement;
                            }
                            break;
                        case XmlNodeType.EndElement:
                            if (include)
                            {
                                writer.WriteEndElement();
                            }
                            else if (recordedDepth == depth)
                            {
                                include = true;
                                recordedDepth = -1;
                            }
                            depth--;
                            break;
                        case XmlNodeType.CDATA:
                            if (include)
                            {
                                writer.WriteCData(Value);
                            }
                            break;
                        case XmlNodeType.Comment:
                            if (include)
                            {
                                writer.WriteComment(Value);
                            }
                            break;
                        case XmlNodeType.Text:
                            if (include)
                            {
                                writer.WriteString(Value);
                            }
                            break;
                        case XmlNodeType.SignificantWhitespace:
                        case XmlNodeType.Whitespace:
                            if (include)
                            {
                                writer.WriteWhitespace(Value);
                            }
                            break;
                        case XmlNodeType.DocumentType:
                        case XmlNodeType.XmlDeclaration:
                            break;
                    }
                }
                while (MoveToNext());
            }

        }       
      
        internal struct XmlTokenEntry
        {
            internal XmlNodeType nodeType;
            internal string prefix;
            internal string localName;
            internal string namespaceUri;

            public bool IsEmptyElement
            {
                get { return Value == null; }
                set { Value = value ? null : ""; }
            }

            public string Value { get; private set; }

            public void Set(XmlNodeType nodeType, string value)
            {
                this.nodeType = nodeType;
                Value = value;
            }

            public void SetAttribute(string prefix, string localName, string namespaceUri, string value)
            {
                nodeType = XmlNodeType.Attribute;
                this.prefix = prefix;
                this.localName = localName;
                this.namespaceUri = namespaceUri;
                Value = value;
            }

            public void SetElement(string prefix, string localName, string namespaceUri, bool isEmptyElement)
            {
                nodeType = XmlNodeType.Element;
                this.prefix = prefix;
                this.localName = localName;
                this.namespaceUri = namespaceUri;
                IsEmptyElement = isEmptyElement;
            }
        }
    }
}
