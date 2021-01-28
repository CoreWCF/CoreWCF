// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Security.Cryptography;
using System.Xml;

namespace CoreWCF.Security
{
    internal class EncryptedData : EncryptedType
    {
        internal static readonly XmlDictionaryString ElementName = XD.XmlEncryptionDictionary.EncryptedData;
        internal static readonly string ElementType = XmlEncryptionStrings.ElementType;
        internal static readonly string ContentType = XmlEncryptionStrings.ContentType;
        private SymmetricAlgorithm _algorithm;
        private byte[] _decryptedBuffer;
        private ArraySegment<byte> _buffer;
        private byte[] _iv;
        private byte[] _cipherText;

        protected override XmlDictionaryString OpeningElementName
        {
            get { return ElementName; }
        }

        private void EnsureDecryptionSet()
        {
            if (State == EncryptionState.DecryptionSetup)
            {
                SetPlainText();
            }
            else if (State != EncryptionState.Decrypted)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.BadEncryptionState));
            }
        }

        protected override void ForceEncryption()
        {
            CryptoHelper.GenerateIVAndEncrypt(_algorithm, _buffer, out _iv, out _cipherText);
            State = EncryptionState.Encrypted;
            _buffer = new ArraySegment<byte>(CryptoHelper.EmptyBuffer);
        }

        public byte[] GetDecryptedBuffer()
        {
            EnsureDecryptionSet();
            return _decryptedBuffer;
        }

        protected override void ReadCipherData(XmlDictionaryReader reader)
        {
            _cipherText = reader.ReadContentAsBase64();
        }

        protected override void ReadCipherData(XmlDictionaryReader reader, long maxBufferSize)
        {
            _cipherText = SecurityUtils.ReadContentAsBase64(reader, maxBufferSize);
        }

        private void SetPlainText()
        {
            _decryptedBuffer = CryptoHelper.ExtractIVAndDecrypt(_algorithm, _cipherText, 0, _cipherText.Length);
            State = EncryptionState.Decrypted;
        }

        public void SetUpDecryption(SymmetricAlgorithm algorithm)
        {
            if (State != EncryptionState.Read)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.BadEncryptionState));
            }
            if (algorithm == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(algorithm));
            }
            _algorithm = algorithm;
            State = EncryptionState.DecryptionSetup;
        }

        public void SetUpEncryption(SymmetricAlgorithm algorithm, ArraySegment<byte> buffer)
        {
            if (State != EncryptionState.New)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.BadEncryptionState));
            }
            if (algorithm == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(algorithm));
            }
            _algorithm = algorithm;
            _buffer = buffer;
            State = EncryptionState.EncryptionSetup;
        }

        protected override void WriteCipherData(XmlDictionaryWriter writer)
        {
            writer.WriteBase64(_iv, 0, _iv.Length);
            writer.WriteBase64(_cipherText, 0, _cipherText.Length);
        }
    }
}
