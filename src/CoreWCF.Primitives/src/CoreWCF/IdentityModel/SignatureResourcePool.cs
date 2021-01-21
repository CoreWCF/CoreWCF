// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Xml;

namespace CoreWCF.IdentityModel
{
    // for sequential use by one thread
    internal sealed class SignatureResourcePool
    {
        private const int BufferSize = 64;
        private HashStream hashStream;
        private HashAlgorithm hashAlgorithm;
        private XmlDictionaryWriter utf8Writer;
        private byte[] encodingBuffer;
        private char[] base64Buffer;

        public char[] TakeBase64Buffer()
        {
            if (base64Buffer == null)
            {
                base64Buffer = new char[BufferSize];
            }
            return base64Buffer;
        }

        public byte[] TakeEncodingBuffer()
        {
            if (encodingBuffer == null)
            {
                encodingBuffer = new byte[BufferSize];
            }
            return encodingBuffer;
        }

        public HashAlgorithm TakeHashAlgorithm(string algorithm)
        {
            if (hashAlgorithm == null)
            {
                if (string.IsNullOrEmpty(algorithm))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(algorithm, SR.Format("EmptyOrNullArgumentString", "algorithm"));
                }

                hashAlgorithm = CryptoHelper.CreateHashAlgorithm(algorithm);
            }
            else
            {
                hashAlgorithm.Initialize();
            }

            return hashAlgorithm;
        }

        public HashStream TakeHashStream(HashAlgorithm hash)
        {
            if (hashStream == null)
            {
                hashStream = new HashStream(hash);
            }
            else
            {
                hashStream.Reset(hash);
            }
            return hashStream;
        }

        public HashStream TakeHashStream(string algorithm)
        {
            return TakeHashStream(TakeHashAlgorithm(algorithm));
        }
#if NO
        public XmlC14NWriter TakeIntegratedWriter(Stream stream)
        {
            return TakeIntegratedWriter(stream, false, null);
        }
 
        public XmlC14NWriter TakeIntegratedWriter(Stream stream, bool includeComments, string[] inclusivePrefixes)
        {
            if (this.integratedWriter == null)
            {
                this.integratedWriter = new XmlC14NWriter(stream, includeComments, inclusivePrefixes);
            }
            else
            {
                this.integratedWriter.SetOutput(stream, includeComments, inclusivePrefixes);
            }
            return this.integratedWriter;
        }
#endif

        public XmlDictionaryWriter TakeUtf8Writer()
        {
            if (utf8Writer == null)
            {
                utf8Writer = XmlDictionaryWriter.CreateTextWriter(Stream.Null, Encoding.UTF8, false);
            }
            else
            {
                ((IXmlTextWriterInitializer)utf8Writer).SetOutput(Stream.Null, Encoding.UTF8, false);
            }
            return utf8Writer;
        }
    }
}
