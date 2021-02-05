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
        private bool _closeReadersAfterProcessing;
        private XmlReader _reader;
        private string[] _inclusivePrefixes;
        private bool _includeComments;

        public bool CloseReadersAfterProcessing
        {
            get
            {
                return this._closeReadersAfterProcessing;
            }
            set
            {
                this._closeReadersAfterProcessing = value;
            }
        }

        public bool IncludeComments
        {
            get
            {
                return this._includeComments;
            }
            set
            {
                this._includeComments = value;
            }
        }

        public string[] GetInclusivePrefixes()
        {
            return this._inclusivePrefixes;
        }

        public void Reset()
        {
            this._reader = (XmlReader)null;
        }

        public void SetInclusivePrefixes(string[] inclusivePrefixes)
        {
            this._inclusivePrefixes = inclusivePrefixes;
        }

        public void SetInput(Stream stream)
        {
            if (stream == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(stream));
            this._reader = XmlReader.Create(stream);
        }

        public void SetInput(XmlReader reader)
        {
            if (reader == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(reader));
            this._reader = reader;
        }

        public byte[] GetBytes()
        {
            return this.GetMemoryStream().ToArray();
        }

        public MemoryStream GetMemoryStream()
        {
            MemoryStream memoryStream = new MemoryStream();
            this.WriteTo((Stream)memoryStream);
            memoryStream.Seek(0L, SeekOrigin.Begin);
            return memoryStream;
        }

        public void WriteTo(HashAlgorithm hashAlgorithm)
        {
            this.WriteTo((Stream)new HashStream(hashAlgorithm));
        }

        public void WriteTo(Stream canonicalStream)
        {
            if (this._reader == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError((Exception)new InvalidOperationException(SR.Format("NoInputIsSetForCanonicalization")));
            if (this._reader is XmlDictionaryReader reader && reader.CanCanonicalize)
            {
                int content = (int)reader.MoveToContent();
                reader.StartCanonicalization(canonicalStream, this._includeComments, this._inclusivePrefixes);
                reader.Skip();
                reader.EndCanonicalization();
            }
            else
            {
                XmlDictionaryWriter textWriter = XmlDictionaryWriter.CreateTextWriter(Stream.Null);
                if (this._inclusivePrefixes != null)
                {
                    textWriter.WriteStartElement("a", this._reader.LookupNamespace(string.Empty));
                    for (int index = 0; index < this._inclusivePrefixes.Length; ++index)
                    {
                        string namespaceUri = this._reader.LookupNamespace(this._inclusivePrefixes[index]);
                        if (namespaceUri != null)
                            textWriter.WriteXmlnsAttribute(this._inclusivePrefixes[index], namespaceUri);
                    }
                }
                textWriter.StartCanonicalization(canonicalStream, this._includeComments, this._inclusivePrefixes);
                //TODO check the use of wrappedreader
                // if (this.reader is WrappedReader)
                //  ((WrappedReader) this.reader).XmlTokens.GetWriter().WriteTo(textWriter, new DictionaryManager());
                // else
                textWriter.WriteNode(this._reader, false);
                textWriter.Flush();
                textWriter.EndCanonicalization();
                if (this._inclusivePrefixes != null)
                    textWriter.WriteEndElement();
                textWriter.Close();
            }
            if (this._closeReadersAfterProcessing)
                this._reader.Close();
            this._reader = (XmlReader)null;
        }
    }
}
