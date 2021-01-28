// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.ObjectModel;
using System.Globalization;
using System.Xml;
using CoreWCF.Channels;

namespace CoreWCF
{
    //[Serializable]
    internal class MustUnderstandSoapException : CommunicationException
    {
        private readonly EnvelopeVersion _envelopeVersion;

        public MustUnderstandSoapException(Collection<MessageHeaderInfo> notUnderstoodHeaders, EnvelopeVersion envelopeVersion)
        {
            NotUnderstoodHeaders = notUnderstoodHeaders;
            _envelopeVersion = envelopeVersion;
        }

        public Collection<MessageHeaderInfo> NotUnderstoodHeaders { get; }
        public EnvelopeVersion EnvelopeVersion { get { return _envelopeVersion; } }

        internal Message ProvideFault(MessageVersion messageVersion)
        {
            string name = NotUnderstoodHeaders[0].Name;
            string ns = NotUnderstoodHeaders[0].Namespace;
            FaultCode code = new FaultCode(MessageStrings.MustUnderstandFault, _envelopeVersion.Namespace);
            FaultReason reason = new FaultReason(SR.Format(SR.SFxHeaderNotUnderstood, name, ns), CultureInfo.CurrentCulture);
            MessageFault fault = MessageFault.CreateFault(code, reason);
            string faultAction = messageVersion.Addressing.DefaultFaultAction;
            Message message = CoreWCF.Channels.Message.CreateMessage(messageVersion, fault, faultAction);
            if (_envelopeVersion == EnvelopeVersion.Soap12)
            {
                AddNotUnderstoodHeaders(message.Headers);
            }
            return message;
        }

        private void AddNotUnderstoodHeaders(MessageHeaders headers)
        {
            for (int i = 0; i < NotUnderstoodHeaders.Count; ++i)
            {
                headers.Add(new NotUnderstoodHeader(NotUnderstoodHeaders[i].Name, NotUnderstoodHeaders[i].Namespace));
            }
        }

        private class NotUnderstoodHeader : MessageHeader
        {
            private readonly string _notUnderstoodName;
            private readonly string _notUnderstoodNs;

            public NotUnderstoodHeader(string name, string ns)
            {
                _notUnderstoodName = name;
                _notUnderstoodNs = ns;
            }

            public override string Name
            {
                get { return Message12Strings.NotUnderstood; }
            }

            public override string Namespace
            {
                get { return Message12Strings.Namespace; }
            }

            protected override void OnWriteStartHeader(XmlDictionaryWriter writer, MessageVersion messageVersion)
            {
                writer.WriteStartElement(Name, Namespace);
                writer.WriteXmlnsAttribute(null, _notUnderstoodNs);
                writer.WriteStartAttribute(Message12Strings.QName);
                writer.WriteQualifiedName(_notUnderstoodName, _notUnderstoodNs);
                writer.WriteEndAttribute();
            }

            protected override void OnWriteHeaderContents(XmlDictionaryWriter writer, MessageVersion messageVersion)
            {
                // empty
            }
        }
    }
}