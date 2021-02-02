// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Xml;
using CoreWCF.Channels;
using CoreWCF.Description;
using ISignatureValueSecurityElement = CoreWCF.IdentityModel.ISignatureValueSecurityElement;

namespace CoreWCF.Security
{
    public abstract class SecurityVersion
    {
        internal SecurityVersion(XmlDictionaryString headerName, XmlDictionaryString headerNamespace, XmlDictionaryString headerPrefix)
        {
            HeaderName = headerName;
            HeaderNamespace = headerNamespace;
            HeaderPrefix = headerPrefix;
        }

        internal XmlDictionaryString HeaderName { get; }

        internal XmlDictionaryString HeaderNamespace { get; }

        internal XmlDictionaryString HeaderPrefix { get; }

        internal abstract XmlDictionaryString FailedAuthenticationFaultCode
        {
            get;
        }

        internal abstract XmlDictionaryString InvalidSecurityFaultCode
        {
            get;
        }
        public static SecurityVersion WSSecurity10 => SecurityVersion10.Instance;


        public static SecurityVersion WSSecurity11 => SecurityVersion11.Instance;

        internal static SecurityVersion Default => WSSecurity11;

        internal abstract ReceiveSecurityHeader CreateReceiveSecurityHeader(Message message,
            string actor, bool mustUnderstand, bool relay,
            SecurityStandardsManager standardsManager,
            SecurityAlgorithmSuite algorithmSuite,
            MessageDirection direction,
            int headerIndex);

        internal abstract SendSecurityHeader CreateSendSecurityHeader(Message message,
            string actor, bool mustUnderstand, bool relay,
            SecurityStandardsManager standardsManager,
            SecurityAlgorithmSuite algorithmSuite,
            MessageDirection direction);

        internal bool DoesMessageContainSecurityHeader(Message message)
        {
            return message.Headers.FindHeader(HeaderName.Value, HeaderNamespace.Value) >= 0;
        }

        internal int FindIndexOfSecurityHeader(Message message, string[] actors)
        {
            return message.Headers.FindHeader(HeaderName.Value, HeaderNamespace.Value, actors);
        }

        internal virtual bool IsReaderAtSignatureConfirmation(XmlDictionaryReader reader)
        {
            return false;
        }

        // The security always look for Empty soap role.  If not found, we will also look for Ultimate actors (next incl).
        // In the future, till we support intermediary scenario, we should refactor this api to do not take actor parameter.
        internal ReceiveSecurityHeader TryCreateReceiveSecurityHeader(Message message,
            string actor,
            SecurityStandardsManager standardsManager,
            SecurityAlgorithmSuite algorithmSuite, MessageDirection direction)
        {
            int headerIndex = message.Headers.FindHeader(HeaderName.Value, HeaderNamespace.Value, actor);
            if (headerIndex < 0 && string.IsNullOrEmpty(actor))
            {
                headerIndex = message.Headers.FindHeader(HeaderName.Value, HeaderNamespace.Value, message.Version.Envelope.UltimateDestinationActorValues);
            }

            if (headerIndex < 0)
            {
                return null;
            }
            MessageHeaderInfo headerInfo = message.Headers[headerIndex];
            return CreateReceiveSecurityHeader(message,
                headerInfo.Actor, headerInfo.MustUnderstand, headerInfo.Relay,
                standardsManager, algorithmSuite,
                direction, headerIndex);
        }

        internal virtual void WriteSignatureConfirmation(XmlDictionaryWriter writer, string id, byte[] signatureConfirmation)
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(
                SR.Format(SR.SignatureConfirmationNotSupported)));
        }

        internal void WriteStartHeader(XmlDictionaryWriter writer)
        {
            writer.WriteStartElement(HeaderPrefix.Value, HeaderName, HeaderNamespace);
        }

        internal virtual ISignatureValueSecurityElement ReadSignatureConfirmation(XmlDictionaryReader reader)
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.SignatureConfirmationNotSupported));
        }

        private class SecurityVersion10 : SecurityVersion
        {
            protected SecurityVersion10() : base(XD.SecurityJan2004Dictionary.Security, XD.SecurityJan2004Dictionary.Namespace, XD.SecurityJan2004Dictionary.Prefix)
            {
            }

            public static SecurityVersion10 Instance { get; } = new SecurityVersion10();

            internal override SendSecurityHeader CreateSendSecurityHeader(Message message,
                string actor, bool mustUnderstand, bool relay,
                SecurityStandardsManager standardsManager,
                SecurityAlgorithmSuite algorithmSuite,
                MessageDirection direction)
            {
                return new WSSecurityOneDotZeroSendSecurityHeader(message, actor, mustUnderstand, relay, standardsManager, algorithmSuite, direction);
            }

            internal override ReceiveSecurityHeader CreateReceiveSecurityHeader(Message message,
                string actor, bool mustUnderstand, bool relay,
                SecurityStandardsManager standardsManager,
                SecurityAlgorithmSuite algorithmSuite,
                MessageDirection direction,
                int headerIndex)
            {
                return new WSSecurityOneDotZeroReceiveSecurityHeader(
                    message,
                    actor, mustUnderstand, relay,
                    standardsManager,
                    algorithmSuite, headerIndex, direction);
            }

            public override string ToString()
            {
                return "WSSecurity10";
            }

            internal override XmlDictionaryString FailedAuthenticationFaultCode => XD.SecurityJan2004Dictionary.FailedAuthenticationFaultCode;

            internal override XmlDictionaryString InvalidSecurityFaultCode => XD.SecurityJan2004Dictionary.InvalidSecurityFaultCode;
        }

        private sealed class SecurityVersion11 : SecurityVersion10
        {
            private SecurityVersion11()
                : base()
            {
            }

            public static new SecurityVersion11 Instance { get; } = new SecurityVersion11();

            internal bool SupportsSignatureConfirmation => true;

            internal override ReceiveSecurityHeader CreateReceiveSecurityHeader(Message message,
                string actor, bool mustUnderstand, bool relay,
                SecurityStandardsManager standardsManager,
                SecurityAlgorithmSuite algorithmSuite,
                MessageDirection direction,
                int headerIndex)
            {
                return new WSSecurityOneDotOneReceiveSecurityHeader(
                    message,
                    actor, mustUnderstand, relay,
                    standardsManager,
                    algorithmSuite, headerIndex, direction);
            }

            internal override SendSecurityHeader CreateSendSecurityHeader(Message message,
                string actor, bool mustUnderstand, bool relay,
                SecurityStandardsManager standardsManager,
                SecurityAlgorithmSuite algorithmSuite, MessageDirection direction)
            {
                return new WSSecurityOneDotOneSendSecurityHeader(message, actor, mustUnderstand, relay, standardsManager, algorithmSuite, direction);
            }

            internal override bool IsReaderAtSignatureConfirmation(XmlDictionaryReader reader)
            {
                return reader.IsStartElement(XD.SecurityXXX2005Dictionary.SignatureConfirmation, XD.SecurityXXX2005Dictionary.Namespace);
            }

            internal override ISignatureValueSecurityElement ReadSignatureConfirmation(XmlDictionaryReader reader)
            {
                reader.MoveToStartElement(XD.SecurityXXX2005Dictionary.SignatureConfirmation, XD.SecurityXXX2005Dictionary.Namespace);
                bool isEmptyElement = reader.IsEmptyElement;
                string id = XmlHelper.GetRequiredNonEmptyAttribute(reader, XD.UtilityDictionary.IdAttribute, XD.UtilityDictionary.Namespace);
                byte[] signatureValue = XmlHelper.GetRequiredBase64Attribute(reader, XD.SecurityXXX2005Dictionary.ValueAttribute, null);
                reader.ReadStartElement();
                if (!isEmptyElement)
                {
                    reader.ReadEndElement();
                }
                return new SignatureConfirmationElement(id, signatureValue, this);
            }

            internal override void WriteSignatureConfirmation(XmlDictionaryWriter writer, string id, byte[] signature)
            {
                if (id == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(id));
                }
                if (signature == null)
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(signature));
                }
                writer.WriteStartElement(XD.SecurityXXX2005Dictionary.Prefix.Value, XD.SecurityXXX2005Dictionary.SignatureConfirmation, XD.SecurityXXX2005Dictionary.Namespace);
                writer.WriteAttributeString(XD.UtilityDictionary.Prefix.Value, XD.UtilityDictionary.IdAttribute, XD.UtilityDictionary.Namespace, id);
                writer.WriteStartAttribute(XD.SecurityXXX2005Dictionary.ValueAttribute, null);
                writer.WriteBase64(signature, 0, signature.Length);
                writer.WriteEndAttribute();
                writer.WriteEndElement();
            }

            public override string ToString()
            {
                return "WSSecurity11";
            }
        }
    }
}
