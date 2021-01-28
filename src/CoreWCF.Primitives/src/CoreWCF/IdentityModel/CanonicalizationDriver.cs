using System;
using System.IO;
using System.Security.Cryptography;
using System.Xml;

namespace CoreWCF.IdentityModel
{
    internal sealed class CanonicalizationDriver
    {
        private bool closeReadersAfterProcessing;
        private XmlReader reader;
        private string[] inclusivePrefixes;
        private bool includeComments;

        public bool CloseReadersAfterProcessing
        {
            get
            {
                return this.closeReadersAfterProcessing;
            }
            set
            {
                this.closeReadersAfterProcessing = value;
            }
        }

        public bool IncludeComments
        {
            get
            {
                return this.includeComments;
            }
            set
            {
                this.includeComments = value;
            }
        }

        public string[] GetInclusivePrefixes()
        {
            return this.inclusivePrefixes;
        }

        public void Reset()
        {
            this.reader = (XmlReader)null;
        }

        public void SetInclusivePrefixes(string[] inclusivePrefixes)
        {
            this.inclusivePrefixes = inclusivePrefixes;
        }

        public void SetInput(Stream stream)
        {
            if (stream == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(stream));
            this.reader = XmlReader.Create(stream);
        }

        public void SetInput(XmlReader reader)
        {
            if (reader == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(reader));
            this.reader = reader;
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
            if (this.reader == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError((Exception)new InvalidOperationException(SR.Format("NoInputIsSetForCanonicalization")));
            if (this.reader is XmlDictionaryReader reader && reader.CanCanonicalize)
            {
                int content = (int)reader.MoveToContent();
                reader.StartCanonicalization(canonicalStream, this.includeComments, this.inclusivePrefixes);
                reader.Skip();
                reader.EndCanonicalization();
            }
            else
            {
                XmlDictionaryWriter textWriter = XmlDictionaryWriter.CreateTextWriter(Stream.Null);
                if (this.inclusivePrefixes != null)
                {
                    textWriter.WriteStartElement("a", this.reader.LookupNamespace(string.Empty));
                    for (int index = 0; index < this.inclusivePrefixes.Length; ++index)
                    {
                        string namespaceUri = this.reader.LookupNamespace(this.inclusivePrefixes[index]);
                        if (namespaceUri != null)
                            textWriter.WriteXmlnsAttribute(this.inclusivePrefixes[index], namespaceUri);
                    }
                }
                textWriter.StartCanonicalization(canonicalStream, this.includeComments, this.inclusivePrefixes);
                //TODO check the use of wrappedreader
                // if (this.reader is WrappedReader)
                //  ((WrappedReader) this.reader).XmlTokens.GetWriter().WriteTo(textWriter, new DictionaryManager());
                // else
                textWriter.WriteNode(this.reader, false);
                textWriter.Flush();
                textWriter.EndCanonicalization();
                if (this.inclusivePrefixes != null)
                    textWriter.WriteEndElement();
                textWriter.Close();
            }
            if (this.closeReadersAfterProcessing)
                this.reader.Close();
            this.reader = (XmlReader)null;
        }
    }
}
