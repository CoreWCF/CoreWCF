using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Selectors;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Runtime;
using System.Runtime.CompilerServices;
using System.Xml;
using DictionaryManager = CoreWCF.IdentityModel.DictionaryManager;
using ISecurityElement = CoreWCF.IdentityModel.ISecurityElement;
using System;
using System.Runtime;


namespace CoreWCF.Security
{

    sealed class EncryptedKey : EncryptedType
    {
        internal static readonly XmlDictionaryString CarriedKeyElementName = XD.XmlEncryptionDictionary.CarriedKeyName;
        internal static readonly XmlDictionaryString ElementName = XD.XmlEncryptionDictionary.EncryptedKey;
        internal static readonly XmlDictionaryString RecipientAttribute = XD.XmlEncryptionDictionary.Recipient;

        string carriedKeyName;
        string recipient;
        ReferenceList referenceList;
        byte[] wrappedKey;

        public string CarriedKeyName
        {
            get { return this.carriedKeyName; }
            set { this.carriedKeyName = value; }
        }

        public string Recipient
        {
            get { return this.recipient; }
            set { this.recipient = value; }
        }

        public ReferenceList ReferenceList
        {
            get { return this.referenceList; }
            set { this.referenceList = value; }
        }

        protected override XmlDictionaryString OpeningElementName
        {
            get { return ElementName; }
        }

        public WSSecurityTokenSerializer SecurityTokenSerializer { get; internal set; }

        protected override void ForceEncryption()
        {
            // no work to be done here since, unlike bulk encryption, key wrapping is done eagerly
        }

        public byte[] GetWrappedKey()
        {
            if (this.State == EncryptionState.New)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.BadEncryptionState)));
            }
            return this.wrappedKey;
        }

        public void SetUpKeyWrap(byte[] wrappedKey)
        {
            if (this.State != EncryptionState.New)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.Format(SR.BadEncryptionState)));
            }
            if (wrappedKey == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("wrappedKey");
            }
            this.wrappedKey = wrappedKey;
            this.State = EncryptionState.Encrypted;
        }

        protected override void ReadAdditionalAttributes(XmlDictionaryReader reader)
        {
            this.recipient = reader.GetAttribute(RecipientAttribute, null);
        }

        protected override void ReadAdditionalElements(XmlDictionaryReader reader)
        {
            if (reader.IsStartElement(ReferenceList.ElementName, EncryptedType.NamespaceUri))
            {
                this.referenceList = new ReferenceList();
                this.referenceList.ReadFrom(reader);
            }
            if (reader.IsStartElement(CarriedKeyElementName, EncryptedType.NamespaceUri))
            {
                reader.ReadStartElement(CarriedKeyElementName, EncryptedType.NamespaceUri);
                this.carriedKeyName = reader.ReadString();
                reader.ReadEndElement();
            }
        }

        protected override void ReadCipherData(XmlDictionaryReader reader)
        {
            this.wrappedKey = reader.ReadContentAsBase64();
        }

        protected override void ReadCipherData(XmlDictionaryReader reader, long maxBufferSize)
        {
            this.wrappedKey = SecurityUtils.ReadContentAsBase64(reader, maxBufferSize);
        }

        protected override void WriteAdditionalAttributes(XmlDictionaryWriter writer, DictionaryManager dictionaryManager)
        {
            if (this.recipient != null)
            {
                writer.WriteAttributeString(RecipientAttribute, null, this.recipient);
            }
        }

        protected override void WriteAdditionalElements(XmlDictionaryWriter writer, DictionaryManager dictionaryManager)
        {
            if (this.carriedKeyName != null)
            {
                writer.WriteStartElement(CarriedKeyElementName, EncryptedType.NamespaceUri);
                writer.WriteString(this.carriedKeyName);
                writer.WriteEndElement(); // CarriedKeyName
            }
            if (this.referenceList != null)
            {
                this.referenceList.WriteTo(writer, dictionaryManager);
            }
        }

        protected override void WriteCipherData(XmlDictionaryWriter writer)
        {
            writer.WriteBase64(this.wrappedKey, 0, this.wrappedKey.Length);
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

        private string _encoding;
        private EncryptionMethodElement _encryptionMethod;
        private string _id;
        private string _wsuId;
        private SecurityKeyIdentifier _keyIdentifier;
        private string _mimeType;
        private EncryptionState _state;
        private string _type;
        private SecurityTokenSerializer _tokenSerializer;
        private bool _shouldReadXmlReferenceKeyInfoClause;

        protected EncryptedType()
        {
            _encryptionMethod.Init();
            _state = EncryptionState.New;
        }

        public string Encoding
        {
            get
            {
                return _encoding;
            }
            set
            {
                _encoding = value;
            }
        }

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

        public string Id
        {
            get
            {
                return _id;
            }
            set
            {
                _id = value;
            }
        }

        // This is set to true on the client side. And this means that when this knob is set to true and the default serializers on the client side fail 
        // to read the KeyInfo clause from the incoming response message from a service; then the ckient should 
        // try to read the keyInfo clause as GenericXmlSecurityKeyIdentifierClause before throwing.
        public bool ShouldReadXmlReferenceKeyInfoClause
        {
            get
            {
                return _shouldReadXmlReferenceKeyInfoClause;
            }
            set
            {
                _shouldReadXmlReferenceKeyInfoClause = value;
            }
        }

        public string WsuId
        {
            get
            {
                return _wsuId;
            }
            set
            {
                _wsuId = value;
            }
        }

        public SecurityKeyIdentifier KeyIdentifier
        {
            get
            {
                return _keyIdentifier;
            }
            set
            {
                _keyIdentifier = value;
            }
        }

        public string MimeType
        {
            get
            {
                return _mimeType;
            }
            set
            {
                _mimeType = value;
            }
        }

        public string Type
        {
            get
            {
                return _type;
            }
            set
            {
                _type = value;
            }
        }

        protected abstract XmlDictionaryString OpeningElementName
        {
            get;
        }

        protected EncryptionState State
        {
            get
            {
                return _state;
            }
            set
            {
                _state = value;
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
            _encoding = reader.GetAttribute(EncodingAttribute, null);
            _id = reader.GetAttribute(XD.XmlEncryptionDictionary.Id, null) ?? SecurityUniqueId.Create().Value;
            _wsuId = reader.GetAttribute(XD.XmlEncryptionDictionary.Id, XD.UtilityDictionary.Namespace) ?? SecurityUniqueId.Create().Value;
            _mimeType = reader.GetAttribute(MimeTypeAttribute, null);
            _type = reader.GetAttribute(TypeAttribute, null);
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

                if (this.ShouldReadXmlReferenceKeyInfoClause)
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
                    this.KeyIdentifier = _tokenSerializer.ReadKeyIdentifier(localReader);
                }
                catch (Exception e)
                {
                    // In case when the issued token ( custom token) is used as an initiator token; we will fail 
                    // to read the keyIdentifierClause using the plugged in default serializer. So We need to try to read it as an XmlReferencekeyIdentifierClause 
                    // if it is the client side.

                    if (Fx.IsFatal(e) || !this.ShouldReadXmlReferenceKeyInfoClause)
                    {
                        throw;
                    }

                    _keyIdentifier = ReadGenericXmlSecurityKeyIdentifier(XmlDictionaryReader.CreateDictionaryReader(new XmlNodeReader(xml)), e);
                }
            }

            reader.ReadStartElement(CipherDataElementName, EncryptedType.NamespaceUri);
            reader.ReadStartElement(CipherValueElementName, EncryptedType.NamespaceUri);
            if (maxBufferSize == 0)
                ReadCipherData(reader);
            else
                ReadCipherData(reader, maxBufferSize);
            reader.ReadEndElement(); // CipherValue
            reader.ReadEndElement(); // CipherData

            ReadAdditionalElements(reader);
            reader.ReadEndElement(); // OpeningElementName
            this.State = EncryptionState.Read;
        }

        private SecurityKeyIdentifier ReadGenericXmlSecurityKeyIdentifier(XmlDictionaryReader localReader, Exception previousException)
        {
            throw new NotImplementedException();
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
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("writer");
            }
            ValidateWriteState();
            writer.WriteStartElement(XmlEncryptionStrings.Prefix, this.OpeningElementName, NamespaceUri);
            if (_id != null && _id.Length != 0)
            {
                writer.WriteAttributeString(XD.XmlEncryptionDictionary.Id, null, this.Id);
            }
            if (_type != null)
            {
                writer.WriteAttributeString(TypeAttribute, null, this.Type);
            }
            if (_mimeType != null)
            {
                writer.WriteAttributeString(MimeTypeAttribute, null, this.MimeType);
            }
            if (_encoding != null)
            {
                writer.WriteAttributeString(EncodingAttribute, null, this.Encoding);
            }
            WriteAdditionalAttributes(writer, dictionaryManager);
            if (_encryptionMethod.algorithm != null)
            {
                _encryptionMethod.WriteTo(writer);
            }
            if (this.KeyIdentifier != null)
            {
                _tokenSerializer.WriteKeyIdentifier(writer, this.KeyIdentifier);
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
            if (this.State != EncryptionState.New)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new Exception("BadEncryptionState"));
            }
        }

        private void ValidateWriteState()
        {
            if (this.State == EncryptionState.EncryptionSetup)
            {
                ForceEncryption();
            }
            else if (this.State == EncryptionState.New)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new Exception("BadEncryptionState"));
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
                this.algorithm = null;
            }

            public void ReadFrom(XmlDictionaryReader reader)
            {
                reader.MoveToStartElement(ElementName, XD.XmlEncryptionDictionary.Namespace);
                bool isEmptyElement = reader.IsEmptyElement;
                this.algorithm = reader.GetAttribute(XD.XmlSignatureDictionary.Algorithm, null);
                if (this.algorithm == null)
                {
                   // throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityMessageSerializationException(
                   //     string.Format(SRServiceModel.RequiredAttributeMissing, XD.XmlSignatureDictionary.Algorithm.Value, ElementName.Value)));
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
                if (this.algorithmDictionaryString != null)
                {
                    writer.WriteStartAttribute(XD.XmlSignatureDictionary.Algorithm, null);
                    writer.WriteString(this.algorithmDictionaryString);
                    writer.WriteEndAttribute();
                }
                else
                {
                    writer.WriteAttributeString(XD.XmlSignatureDictionary.Algorithm, null, this.algorithm);
                }
                if (this.algorithm == XD.SecurityAlgorithmDictionary.RsaOaepKeyWrap.Value)
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
