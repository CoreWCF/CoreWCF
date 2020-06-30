using CoreWCF.Channels;
using CoreWCF.Description;
using CoreWCF.IdentityModel;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Security.Tokens;
using System;
using System.Security.Cryptography.Xml;
using System.Xml;

namespace CoreWCF.Security
{
    internal class WSSecurityOneDotZeroReceiveSecurityHeader : ReceiveSecurityHeader
    {
        public WSSecurityOneDotZeroReceiveSecurityHeader(Message message, string actor, bool mustUnderstand, bool relay,
            SecurityStandardsManager standardsManager,
            SecurityAlgorithmSuite algorithmSuite,
            int headerIndex,
            MessageDirection transferDirection)
            : base(message, actor, mustUnderstand, relay, standardsManager, algorithmSuite, headerIndex, transferDirection)
        {
        }

        protected override bool IsReaderAtReferenceList(XmlDictionaryReader reader)
        {
            return reader.IsStartElement(ReferenceList.ElementName, ReferenceList.NamespaceUri);
        }

        protected override bool IsReaderAtSignature(XmlDictionaryReader reader)
        {
            return reader.IsStartElement(XD.XmlSignatureDictionary.Signature, XD.XmlSignatureDictionary.Namespace);
        }

        protected override void EnsureDecryptionComplete()
        {
            // noop
        }

        protected override bool IsReaderAtEncryptedKey(XmlDictionaryReader reader)
        {
            return reader.IsStartElement(CoreWCF.XD.XmlEncryptionDictionary.EncryptedKey, CoreWCF.XD.XmlEncryptionDictionary.Namespace);
        }

        protected override bool IsReaderAtEncryptedData(XmlDictionaryReader reader)
        {
            bool encrypted = reader.IsStartElement(CoreWCF.XD.XmlEncryptionDictionary.EncryptedData, CoreWCF.XD.XmlEncryptionDictionary.Namespace);

            if (encrypted == true)
            {
                throw new PlatformNotSupportedException();
            }

            return encrypted;
        }

        protected override bool IsReaderAtSecurityTokenReference(XmlDictionaryReader reader)
        {
            return reader.IsStartElement(XD.SecurityJan2004Dictionary.SecurityTokenReference, XD.SecurityJan2004Dictionary.Namespace);
        }

        protected override EncryptedData ReadSecurityHeaderEncryptedItem(XmlDictionaryReader reader, bool readXmlreferenceKeyInfoClause)
        {
            throw new NotImplementedException();
        }

        protected override byte[] DecryptSecurityHeaderElement(EncryptedData encryptedData, WrappedKeySecurityToken wrappedKeyToken, out SecurityToken encryptionToken)
        {
            throw new NotImplementedException();
        }

        protected override WrappedKeySecurityToken DecryptWrappedKey(XmlDictionaryReader reader)
        {
            throw new NotImplementedException();
        }

        protected override void OnDecryptionOfSecurityHeaderItemRequiringReferenceListEntry(string id)
        {
            throw new NotImplementedException();
        }

        protected override void ExecuteMessageProtectionPass(bool hasAtLeastOneSupportingTokenExpectedToBeSigned)
        {
            throw new NotImplementedException();
        }

        protected override ReferenceList ReadReferenceListCore(XmlDictionaryReader reader)
        {
            throw new NotImplementedException();
        }

        protected override void ProcessReferenceListCore(ReferenceList referenceList, WrappedKeySecurityToken wrappedKeyToken)
        {
            throw new NotImplementedException();
        }

        protected override void ReadSecurityTokenReference(XmlDictionaryReader reader)
        {
            throw new NotImplementedException();
        }

        protected override SignedXml ReadSignatureCore(XmlDictionaryReader signatureReader)
        {
            throw new NotImplementedException();
        }

        protected override SecurityToken VerifySignature(SignedXml signedXml, bool isPrimarySignature, SecurityHeaderTokenResolver resolver, object signatureTarget, string id)
        {
            throw new NotImplementedException();
        }

        protected override bool TryDeleteReferenceListEntry(string id)
        {
            throw new NotImplementedException();
        }
    }
}
