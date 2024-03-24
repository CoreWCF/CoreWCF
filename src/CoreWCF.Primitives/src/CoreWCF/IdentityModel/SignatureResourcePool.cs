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
        private HashStream _hashStream;
        private HashAlgorithm _hashAlgorithm;
        private XmlDictionaryWriter _utf8Writer;
        private byte[] _encodingBuffer;
        private char[] _base64Buffer;
        private CanonicalizationDriver _canonicalizationDriver;

        public char[] TakeBase64Buffer()
        {
            if (_base64Buffer == null)
            {
                _base64Buffer = new char[BufferSize];
            }
            return _base64Buffer;
        }

        public byte[] TakeEncodingBuffer()
        {
            if (_encodingBuffer == null)
            {
                _encodingBuffer = new byte[BufferSize];
            }
            return _encodingBuffer;
        }

        public CanonicalizationDriver TakeCanonicalizationDriver()
        {
            if (_canonicalizationDriver == null)
            {
                _canonicalizationDriver = new CanonicalizationDriver();
            }
            else
            {
                _canonicalizationDriver.Reset();
            }
            return _canonicalizationDriver;
        }

        public HashAlgorithm TakeHashAlgorithm(string algorithm)
        {
            if (_hashAlgorithm == null)
            {
                if (string.IsNullOrEmpty(algorithm))
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(algorithm, SR.Format("EmptyOrNullArgumentString", "algorithm"));
                }

                _hashAlgorithm = CryptoHelper.CreateHashAlgorithm(algorithm);
            }
            else
            {
                _hashAlgorithm.Initialize();
            }

            return _hashAlgorithm;
        }

        public HashStream TakeHashStream(HashAlgorithm hash)
        {
            if (_hashStream == null)
            {
                _hashStream = new HashStream(hash);
            }
            else
            {
                _hashStream.Reset(hash);
            }
            return _hashStream;
        }

        public HashStream TakeHashStream(string algorithm)
        {
            return TakeHashStream(TakeHashAlgorithm(algorithm));
        }

        public XmlDictionaryWriter TakeUtf8Writer()
        {
            if (_utf8Writer == null)
            {
                _utf8Writer = XmlDictionaryWriter.CreateTextWriter(Stream.Null, Encoding.UTF8, false);
            }
            else
            {
                ((IXmlTextWriterInitializer)_utf8Writer).SetOutput(Stream.Null, Encoding.UTF8, false);
            }
            return _utf8Writer;
        }
    }
}
