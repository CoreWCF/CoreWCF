// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Security.Cryptography;
using System.Xml;

namespace CoreWCF.IdentityModel
{
    internal sealed class CanonicalizationDriver
    {
        private XmlReader _reader;
        private string[] _inclusivePrefixes;

        public bool CloseReadersAfterProcessing { get; set; }

        public bool IncludeComments { get; set; }

        public string[] GetInclusivePrefixes()
        {
            return _inclusivePrefixes;
        }

        public void Reset()
        {
            _reader = (XmlReader)null;
        }

        public void SetInclusivePrefixes(string[] inclusivePrefixes)
        {
            _inclusivePrefixes = inclusivePrefixes;
        }

        public void SetInput(Stream stream)
        {
            if (stream == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(stream));
            _reader = XmlReader.Create(stream);
        }

        public void SetInput(XmlReader reader)
        {
            _reader = reader ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(reader));
        }

        public byte[] GetBytes()
        {
            return GetMemoryStream().ToArray();
        }

        public MemoryStream GetMemoryStream()
        {
            MemoryStream memoryStream = new MemoryStream();
            WriteTo((Stream)memoryStream);
            memoryStream.Seek(0L, SeekOrigin.Begin);
            return memoryStream;
        }

        public void WriteTo(HashAlgorithm hashAlgorithm)
        {
            WriteTo((Stream)new HashStream(hashAlgorithm));
        }

        public void WriteTo(Stream canonicalStream)
        {
            if (_reader == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError((Exception)new InvalidOperationException(SR.Format("NoInputIsSetForCanonicalization")));
            if (_reader is XmlDictionaryReader reader && reader.CanCanonicalize)
            {
                _ = (int)reader.MoveToContent();
                reader.StartCanonicalization(canonicalStream, IncludeComments, _inclusivePrefixes);
                reader.Skip();
                reader.EndCanonicalization();
            }
            else
            {
                XmlDictionaryWriter textWriter = XmlDictionaryWriter.CreateTextWriter(Stream.Null);
                if (_inclusivePrefixes != null)
                {
                    textWriter.WriteStartElement("a", _reader.LookupNamespace(string.Empty));
                    for (int index = 0; index < _inclusivePrefixes.Length; ++index)
                    {
                        string namespaceUri = _reader.LookupNamespace(_inclusivePrefixes[index]);
                        if (namespaceUri != null)
                            textWriter.WriteXmlnsAttribute(_inclusivePrefixes[index], namespaceUri);
                    }
                }
                textWriter.StartCanonicalization(canonicalStream, IncludeComments, _inclusivePrefixes);
                //TODO check the use of wrappedreader
                // if (this.reader is WrappedReader)
                //  ((WrappedReader) this.reader).XmlTokens.GetWriter().WriteTo(textWriter, new DictionaryManager());
                // else
                textWriter.WriteNode(_reader, false);
                textWriter.Flush();
                textWriter.EndCanonicalization();
                if (_inclusivePrefixes != null)
                    textWriter.WriteEndElement();
                textWriter.Close();
            }
            if (CloseReadersAfterProcessing)
                _reader.Close();
            _reader = (XmlReader)null;
        }
    }
}
