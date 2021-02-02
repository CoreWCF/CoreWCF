// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Xml;
using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Runtime;
using DictionaryManager = CoreWCF.IdentityModel.DictionaryManager;
using ISecurityElement = CoreWCF.IdentityModel.ISecurityElement;

namespace CoreWCF.Security
{
    internal sealed class EncryptedKey : EncryptedType
    {
        internal static readonly XmlDictionaryString CarriedKeyElementName = XD.XmlEncryptionDictionary.CarriedKeyName;
        internal static readonly XmlDictionaryString ElementName = XD.XmlEncryptionDictionary.EncryptedKey;
        internal static readonly XmlDictionaryString RecipientAttribute = XD.XmlEncryptionDictionary.Recipient;
        private byte[] _wrappedKey;

        public string CarriedKeyName { get; set; }

        public string Recipient { get; set; }

        public ReferenceList ReferenceList { get; set; }

        protected override XmlDictionaryString OpeningElementName => ElementName;

        protected override void ForceEncryption()
        {
            // no work to be done here since, unlike bulk encryption, key wrapping is done eagerly
        }

        public byte[] GetWrappedKey()
        {
            if (State == EncryptionState.New)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.BadEncryptionState));
            }
            return _wrappedKey;
        }

        public void SetUpKeyWrap(byte[] wrappedKey)
        {
            if (State != EncryptionState.New)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.BadEncryptionState));
            }

            _wrappedKey = wrappedKey ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(wrappedKey));
            State = EncryptionState.Encrypted;
        }

        protected override void ReadAdditionalAttributes(XmlDictionaryReader reader)
        {
            Recipient = reader.GetAttribute(RecipientAttribute, null);
        }

        protected override void ReadAdditionalElements(XmlDictionaryReader reader)
        {
            if (reader.IsStartElement(ReferenceList.ElementName, NamespaceUri))
            {
                ReferenceList = new ReferenceList();
                ReferenceList.ReadFrom(reader);
            }
            if (reader.IsStartElement(CarriedKeyElementName, NamespaceUri))
            {
                reader.ReadStartElement(CarriedKeyElementName, NamespaceUri);
                CarriedKeyName = reader.ReadString();
                reader.ReadEndElement();
            }
        }

        protected override void ReadCipherData(XmlDictionaryReader reader)
        {
            _wrappedKey = reader.ReadContentAsBase64();
        }

        protected override void ReadCipherData(XmlDictionaryReader reader, long maxBufferSize)
        {
            _wrappedKey = SecurityUtils.ReadContentAsBase64(reader, maxBufferSize);
        }

        protected override void WriteAdditionalAttributes(XmlDictionaryWriter writer, DictionaryManager dictionaryManager)
        {
            if (Recipient != null)
            {
                writer.WriteAttributeString(RecipientAttribute, null, Recipient);
            }
        }

        protected override void WriteAdditionalElements(XmlDictionaryWriter writer, DictionaryManager dictionaryManager)
        {
            if (CarriedKeyName != null)
            {
                writer.WriteStartElement(CarriedKeyElementName, NamespaceUri);
                writer.WriteString(CarriedKeyName);
                writer.WriteEndElement(); // CarriedKeyName
            }
            if (ReferenceList != null)
            {
                ReferenceList.WriteTo(writer, dictionaryManager);
            }
        }

        protected override void WriteCipherData(XmlDictionaryWriter writer)
        {
            writer.WriteBase64(_wrappedKey, 0, _wrappedKey.Length);
        }
    }

    internal abstract class EncryptedType : ISecurityElement
    {
        internal static readonly XmlDictionaryString NamespaceUri = XD.XmlEncryptionDictionary.Namespace;
        internal static readonly XmlDictionaryString EncodingAttribute = XD.XmlEncryptionDictionary.Encoding;
        internal static readonly XmlDictionaryString MimeTypeAttribute = XD.XmlEncryptionDictionary.MimeType;
        internal static readonly XmlDictionaryString TypeAttribute = XD.XmlEncryptionDictionary.Type;
        internal static readonly XmlDictionaryString CipherDataElementName = XD.XmlEncryptionDictionary.CipherData;
        internal static readonly XmlDictionaryString CipherValueElementName = XD.XmlEncryptionDictionary.CipherValue;
        private EncryptionMethodElement _encryptionMethod;
        private SecurityTokenSerializer _tokenSerializer;

        protected EncryptedType()
        {
            _encryptionMethod.Init();
            State = EncryptionState.New;
            _tokenSerializer = new KeyInfoSerializer(false);
        }

        public string Encoding { get; set; }

        public string EncryptionMethod
        {
            get
            {
                return _encryptionMethod.algorithm;
            }
            set
            {
                _encryptionMethod.algorithm = value;
            }
        }

        public XmlDictionaryString EncryptionMethodDictionaryString
        {
            get
            {
                return _encryptionMethod.algorithmDictionaryString;
            }
            set
            {
                _encryptionMethod.algorithmDictionaryString = value;
            }
        }

        public bool HasId
        {
            get
            {
                return true;
            }
        }

        public string Id { get; set; }

        // This is set to true on the client side. And this means that when this knob is set to true and the default serializers on the client side fail 
        // to read the KeyInfo clause from the incoming response message from a service; then the ckient should 
        // try to read the keyInfo clause as GenericXmlSecurityKeyIdentifierClause before throwing.
        public bool ShouldReadXmlReferenceKeyInfoClause { get; set; }

        public string WsuId { get; set; }

        public SecurityKeyIdentifier KeyIdentifier { get; set; }

        public string MimeType { get; set; }

        public string Type { get; set; }

        protected abstract XmlDictionaryString OpeningElementName
        {
            get;
        }

        protected EncryptionState State { get; set; }

        public SecurityTokenSerializer SecurityTokenSerializer
        {
            get
            {
                return _tokenSerializer;
            }
            set
            {
                _tokenSerializer = value ?? new KeyInfoSerializer(false);
            }
        }

        protected abstract void ForceEncryption();

        protected virtual void ReadAdditionalAttributes(XmlDictionaryReader reader)
        {
        }

        protected virtual void ReadAdditionalElements(XmlDictionaryReader reader)
        {
        }

        protected abstract void ReadCipherData(XmlDictionaryReader reader);
        protected abstract void ReadCipherData(XmlDictionaryReader reader, long maxBufferSize);

        public void ReadFrom(XmlReader reader)
        {
            ReadFrom(reader, 0);
        }

        public void ReadFrom(XmlDictionaryReader reader)
        {
            ReadFrom(reader, 0);
        }

        public void ReadFrom(XmlReader reader, long maxBufferSize)
        {
            ReadFrom(XmlDictionaryReader.CreateDictionaryReader(reader), maxBufferSize);
        }

        public void ReadFrom(XmlDictionaryReader reader, long maxBufferSize)
        {
            ValidateReadState();
            reader.MoveToStartElement(OpeningElementName, NamespaceUri);
            Encoding = reader.GetAttribute(EncodingAttribute, null);
            Id = reader.GetAttribute(XD.XmlEncryptionDictionary.Id, null) ?? SecurityUniqueId.Create().Value;
            WsuId = reader.GetAttribute(XD.XmlEncryptionDictionary.Id, XD.UtilityDictionary.Namespace) ?? SecurityUniqueId.Create().Value;
            MimeType = reader.GetAttribute(MimeTypeAttribute, null);
            Type = reader.GetAttribute(TypeAttribute, null);
            ReadAdditionalAttributes(reader);
            reader.Read();

            if (reader.IsStartElement(EncryptionMethodElement.ElementName, NamespaceUri))
            {
                _encryptionMethod.ReadFrom(reader);
            }

            if (_tokenSerializer.CanReadKeyIdentifier(reader))
            {
                XmlElement xml = null;
                XmlDictionaryReader localReader;

                if (ShouldReadXmlReferenceKeyInfoClause)
                {
                    // We create the dom only when needed to not affect perf.
                    XmlDocument doc = new XmlDocument();
                    xml = (doc.ReadNode(reader) as XmlElement);
                    localReader = XmlDictionaryReader.CreateDictionaryReader(new XmlNodeReader(xml));
                }
                else
                {
                    localReader = reader;
                }

                try
                {
                    KeyIdentifier = _tokenSerializer.ReadKeyIdentifier(localReader);
                }
                catch (Exception e)
                {
                    // In case when the issued token ( custom token) is used as an initiator token; we will fail 
                    // to read the keyIdentifierClause using the plugged in default serializer. So We need to try to read it as an XmlReferencekeyIdentifierClause 
                    // if it is the client side.

                    if (Fx.IsFatal(e) || !ShouldReadXmlReferenceKeyInfoClause)
                    {
                        throw;
                    }

                    KeyIdentifier = ReadGenericXmlSecurityKeyIdentifier(XmlDictionaryReader.CreateDictionaryReader(new XmlNodeReader(xml)), e);
                }
            }

            reader.ReadStartElement(CipherDataElementName, NamespaceUri);
            reader.ReadStartElement(CipherValueElementName, NamespaceUri);
            if (maxBufferSize == 0)
            {
                ReadCipherData(reader);
            }
            else
            {
                ReadCipherData(reader, maxBufferSize);
            }

            reader.ReadEndElement(); // CipherValue
            reader.ReadEndElement(); // CipherData

            ReadAdditionalElements(reader);
            reader.ReadEndElement(); // OpeningElementName
            State = EncryptionState.Read;
        }

        private SecurityKeyIdentifier ReadGenericXmlSecurityKeyIdentifier(XmlDictionaryReader localReader, Exception previousException)
        {
            if (!localReader.IsStartElement(XD.XmlSignatureDictionary.KeyInfo, XD.XmlSignatureDictionary.Namespace))
            {
                return null;
            }

            localReader.ReadStartElement(XD.XmlSignatureDictionary.KeyInfo, XD.XmlSignatureDictionary.Namespace);
            SecurityKeyIdentifier keyIdentifier = new SecurityKeyIdentifier();

            if (localReader.IsStartElement())
            {
                string strId = localReader.GetAttribute(XD.UtilityDictionary.IdAttribute, XD.UtilityDictionary.Namespace);
                XmlDocument doc = new XmlDocument();
                XmlElement keyIdentifierReferenceXml = (doc.ReadNode(localReader) as XmlElement);
                SecurityKeyIdentifierClause clause = new GenericXmlSecurityKeyIdentifierClause(keyIdentifierReferenceXml);
                if (!string.IsNullOrEmpty(strId))
                {
                    clause.Id = strId;
                }

                keyIdentifier.Add(clause);
            }

            if (keyIdentifier.Count == 0)
            {
                throw previousException;
            }

            localReader.ReadEndElement();
            return keyIdentifier;
        }

        protected virtual void WriteAdditionalAttributes(XmlDictionaryWriter writer, DictionaryManager dictionaryManager)
        {
        }

        protected virtual void WriteAdditionalElements(XmlDictionaryWriter writer, DictionaryManager dictionaryManager)
        {
        }

        protected abstract void WriteCipherData(XmlDictionaryWriter writer);

        public void WriteTo(XmlDictionaryWriter writer, DictionaryManager dictionaryManager)
        {
            if (writer == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(writer));
            }
            ValidateWriteState();
            writer.WriteStartElement(XmlEncryptionStrings.Prefix, OpeningElementName, NamespaceUri);
            if (Id != null && Id.Length != 0)
            {
                writer.WriteAttributeString(XD.XmlEncryptionDictionary.Id, null, Id);
            }
            if (Type != null)
            {
                writer.WriteAttributeString(TypeAttribute, null, Type);
            }
            if (MimeType != null)
            {
                writer.WriteAttributeString(MimeTypeAttribute, null, MimeType);
            }
            if (Encoding != null)
            {
                writer.WriteAttributeString(EncodingAttribute, null, Encoding);
            }
            WriteAdditionalAttributes(writer, dictionaryManager);
            if (_encryptionMethod.algorithm != null)
            {
                _encryptionMethod.WriteTo(writer);
            }
            if (KeyIdentifier != null)
            {
                _tokenSerializer.WriteKeyIdentifier(writer, KeyIdentifier);
            }

            writer.WriteStartElement(CipherDataElementName, NamespaceUri);
            writer.WriteStartElement(CipherValueElementName, NamespaceUri);
            WriteCipherData(writer);
            writer.WriteEndElement(); // CipherValue
            writer.WriteEndElement(); // CipherData

            WriteAdditionalElements(writer, dictionaryManager);
            writer.WriteEndElement(); // OpeningElementName
        }

        private void ValidateReadState()
        {
            if (State != EncryptionState.New)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityMessageSerializationException(SR.BadEncryptionState));
            }
        }

        private void ValidateWriteState()
        {
            if (State == EncryptionState.EncryptionSetup)
            {
                ForceEncryption();
            }
            else if (State == EncryptionState.New)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityMessageSerializationException(SR.BadEncryptionState));
            }
        }

        protected enum EncryptionState
        {
            New,
            Read,
            DecryptionSetup,
            Decrypted,
            EncryptionSetup,
            Encrypted
        }

        private struct EncryptionMethodElement
        {
            internal string algorithm;
            internal XmlDictionaryString algorithmDictionaryString;
            internal static readonly XmlDictionaryString ElementName = XD.XmlEncryptionDictionary.EncryptionMethod;

            public void Init()
            {
                algorithm = null;
            }

            public void ReadFrom(XmlDictionaryReader reader)
            {
                reader.MoveToStartElement(ElementName, XD.XmlEncryptionDictionary.Namespace);
                bool isEmptyElement = reader.IsEmptyElement;
                algorithm = reader.GetAttribute(XD.XmlSignatureDictionary.Algorithm, null);
                if (algorithm == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityMessageSerializationException(
                        SR.Format(SR.RequiredAttributeMissing, XD.XmlSignatureDictionary.Algorithm.Value, ElementName.Value)));
                }
                reader.Read();
                if (!isEmptyElement)
                {
                    while (reader.IsStartElement())
                    {
                        reader.Skip();
                    }
                    reader.ReadEndElement();
                }
            }

            public void WriteTo(XmlDictionaryWriter writer)
            {
                writer.WriteStartElement(XmlEncryptionStrings.Prefix, ElementName, XD.XmlEncryptionDictionary.Namespace);
                if (algorithmDictionaryString != null)
                {
                    writer.WriteStartAttribute(XD.XmlSignatureDictionary.Algorithm, null);
                    writer.WriteString(algorithmDictionaryString);
                    writer.WriteEndAttribute();
                }
                else
                {
                    writer.WriteAttributeString(XD.XmlSignatureDictionary.Algorithm, null, algorithm);
                }
                if (algorithm == XD.SecurityAlgorithmDictionary.RsaOaepKeyWrap.Value)
                {
                    writer.WriteStartElement(XmlSignatureStrings.Prefix, XD.XmlSignatureDictionary.DigestMethod, XD.XmlSignatureDictionary.Namespace);
                    writer.WriteStartAttribute(XD.XmlSignatureDictionary.Algorithm, null);
                    writer.WriteString(XD.SecurityAlgorithmDictionary.Sha1Digest);
                    writer.WriteEndAttribute();
                    writer.WriteEndElement();
                }
                writer.WriteEndElement(); // EncryptionMethod
            }
        }
    }
}
