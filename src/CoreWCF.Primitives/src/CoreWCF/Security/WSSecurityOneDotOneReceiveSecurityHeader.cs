// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography;
using System.Xml;
using CoreWCF.Channels;
using CoreWCF.Description;
using CoreWCF.IdentityModel.Tokens;
using CoreWCF.Security.Tokens;

namespace CoreWCF.Security
{
    internal class WSSecurityOneDotOneReceiveSecurityHeader : WSSecurityOneDotZeroReceiveSecurityHeader
    {
        public WSSecurityOneDotOneReceiveSecurityHeader(Message message, string actor, bool mustUnderstand, bool relay,
            SecurityStandardsManager standardsManager,
            SecurityAlgorithmSuite algorithmSuite,
            int headerIndex, MessageDirection direction)
            : base(message, actor, mustUnderstand, relay, standardsManager, algorithmSuite, headerIndex, direction)
        {
        }

        protected override DecryptedHeader DecryptHeader(XmlDictionaryReader reader, WrappedKeySecurityToken wrappedKeyToken)
        {
            // If it is the client, then we may need to read the GenericXmlSecurityKeyIdentoifoer clause while reading EncryptedData. 
            EncryptedHeaderXml headerXml = new EncryptedHeaderXml(Version, MessageDirection == MessageDirection.Output);
            headerXml.SecurityTokenSerializer = StandardsManager.SecurityTokenSerializer;
            headerXml.ReadFrom(reader, MaxReceivedMessageSize);

            // The Encrypted Headers MustUnderstand, Relay and Actor attributes should match the
            // Security Headers value.
            if (headerXml.MustUnderstand != MustUnderstand)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.EncryptedHeaderAttributeMismatch, XD.MessageDictionary.MustUnderstand.Value, headerXml.MustUnderstand, MustUnderstand)));

            if (headerXml.Relay != Relay)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.EncryptedHeaderAttributeMismatch, XD.Message12Dictionary.Relay.Value, headerXml.Relay, Relay)));

            if (headerXml.Actor != Actor)
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new MessageSecurityException(SR.Format(SR.EncryptedHeaderAttributeMismatch, Version.Envelope.DictionaryActor, headerXml.Actor, Actor)));

            SecurityToken token;
            if (wrappedKeyToken == null)
            {
                token = ResolveKeyIdentifier(headerXml.KeyIdentifier, CombinedPrimaryTokenResolver, false);
            }
            else
            {
                token = wrappedKeyToken;
            }
            RecordEncryptionToken(token);
            using (SymmetricAlgorithm algorithm = CreateDecryptionAlgorithm(token, headerXml.EncryptionMethod, AlgorithmSuite))
            {
                headerXml.SetUpDecryption(algorithm);
                return new DecryptedHeader(
                    headerXml.GetDecryptedBuffer(),
                    SecurityVerifiedMessage.GetEnvelopeAttributes(), SecurityVerifiedMessage.GetHeaderAttributes(),
                    Version, StandardsManager.IdManager, ReaderQuotas);
            }
        }
    }
}
