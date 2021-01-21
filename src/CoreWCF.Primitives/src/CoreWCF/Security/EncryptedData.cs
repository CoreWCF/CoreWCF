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
        private SymmetricAlgorithm algorithm;
        private byte[] decryptedBuffer;
        private ArraySegment<byte> buffer;
        private byte[] iv;
        private byte[] cipherText;

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
            CryptoHelper.GenerateIVAndEncrypt(algorithm, buffer, out iv, out cipherText);
            State = EncryptionState.Encrypted;
            buffer = new ArraySegment<byte>(CryptoHelper.EmptyBuffer);
        }

        public byte[] GetDecryptedBuffer()
        {
            EnsureDecryptionSet();
            return decryptedBuffer;
        }

        protected override void ReadCipherData(XmlDictionaryReader reader)
        {
            cipherText = reader.ReadContentAsBase64();
        }

        protected override void ReadCipherData(XmlDictionaryReader reader, long maxBufferSize)
        {
            cipherText = SecurityUtils.ReadContentAsBase64(reader, maxBufferSize);
        }

        private void SetPlainText()
        {
            decryptedBuffer = CryptoHelper.ExtractIVAndDecrypt(algorithm, cipherText, 0, cipherText.Length);
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
            this.algorithm = algorithm;
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
            this.algorithm = algorithm;
            this.buffer = buffer;
            State = EncryptionState.EncryptionSetup;
        }

        protected override void WriteCipherData(XmlDictionaryWriter writer)
        {
            writer.WriteBase64(iv, 0, iv.Length);
            writer.WriteBase64(cipherText, 0, cipherText.Length);
        }
    }
}
