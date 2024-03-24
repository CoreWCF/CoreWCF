
using System.Xml;
using CoreWCF.Channels;
using CoreWCF;

namespace CoreWCF.Security
{
    internal sealed class DecryptedHeader : ReadableMessageHeader
    {
        private XmlDictionaryReader _cachedReader;
        private readonly byte[] decryptedBuffer;
        private readonly string id;
        private readonly string name;
        private readonly string namespaceUri;
        private readonly string actor;
        private readonly bool mustUnderstand;
        private readonly bool relay;
        private readonly bool isRefParam;
        private readonly MessageVersion version;
        private readonly XmlAttributeHolder[] envelopeAttributes;
        private readonly XmlAttributeHolder[] headerAttributes;
        private readonly XmlDictionaryReaderQuotas quotas;

        public DecryptedHeader(byte[] decryptedBuffer,
            XmlAttributeHolder[] envelopeAttributes, XmlAttributeHolder[] headerAttributes,
            MessageVersion version, SignatureTargetIdManager idManager, XmlDictionaryReaderQuotas quotas)
        {
            if (quotas == null)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("quotas");

            this.decryptedBuffer = decryptedBuffer;
            this.version = version;
            this.envelopeAttributes = envelopeAttributes;
            this.headerAttributes = headerAttributes;
            this.quotas = quotas;

            XmlDictionaryReader reader = CreateReader();
            reader.MoveToStartElement();

            name = reader.LocalName;
            namespaceUri = reader.NamespaceURI;
            MessageHeader.GetHeaderAttributes(reader, version, out actor, out mustUnderstand, out relay, out isRefParam);
            id = idManager.ExtractId(reader);

            _cachedReader = reader;
        }

        public override string Actor
        {
            get
            {
                return actor;
            }
        }

        public string Id
        {
            get
            {
                return id;
            }
        }

        public override bool IsReferenceParameter
        {
            get
            {
                return isRefParam;
            }
        }
        
        public override bool MustUnderstand
        {
            get
            {
                return mustUnderstand;
            }
        }

        public override string Name
        {
            get
            {
                return name;
            }
        }

        public override string Namespace
        {
            get
            {
                return namespaceUri;
            }
        }

        public override bool Relay
        {
            get
            {
                return relay;
            }
        }

        private XmlDictionaryReader CreateReader()
        {
            return ContextImportHelper.CreateSplicedReader(
                decryptedBuffer,
                envelopeAttributes,
                headerAttributes, null, quotas);
        }

        public override XmlDictionaryReader GetHeaderReader()
        {
            if (_cachedReader != null)
            {
                XmlDictionaryReader cachedReader = this._cachedReader;
                this._cachedReader = null;
                return cachedReader;
            }
            XmlDictionaryReader reader = CreateReader();
            reader.MoveToContent();
            return reader;
        }

        public override bool IsMessageVersionSupported(MessageVersion messageVersion)
        {
            return version.Equals( messageVersion );
        }
    }
}
