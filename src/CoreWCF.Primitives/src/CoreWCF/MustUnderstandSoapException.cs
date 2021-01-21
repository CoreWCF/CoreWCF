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
        // for serialization
        //public MustUnderstandSoapException() { }
        //protected MustUnderstandSoapException(SerializationInfo info, StreamingContext context) : base(info, context) { }


        Collection<MessageHeaderInfo> notUnderstoodHeaders;
        EnvelopeVersion envelopeVersion;

        public MustUnderstandSoapException(Collection<MessageHeaderInfo> notUnderstoodHeaders, EnvelopeVersion envelopeVersion)
        {
            this.notUnderstoodHeaders = notUnderstoodHeaders;
            this.envelopeVersion = envelopeVersion;
        }

        public Collection<MessageHeaderInfo> NotUnderstoodHeaders { get { return notUnderstoodHeaders; } }
        public EnvelopeVersion EnvelopeVersion { get { return envelopeVersion; } }

        internal Message ProvideFault(MessageVersion messageVersion)
        {
            string name = notUnderstoodHeaders[0].Name;
            string ns = notUnderstoodHeaders[0].Namespace;
            FaultCode code = new FaultCode(MessageStrings.MustUnderstandFault, envelopeVersion.Namespace);
            FaultReason reason = new FaultReason(SR.Format(SR.SFxHeaderNotUnderstood, name, ns), CultureInfo.CurrentCulture);
            MessageFault fault = MessageFault.CreateFault(code, reason);
            string faultAction = messageVersion.Addressing.DefaultFaultAction;
            Message message = CoreWCF.Channels.Message.CreateMessage(messageVersion, fault, faultAction);
            if (envelopeVersion == EnvelopeVersion.Soap12)
            {
                AddNotUnderstoodHeaders(message.Headers);
            }
            return message;
        }

        void AddNotUnderstoodHeaders(MessageHeaders headers)
        {
            for (int i = 0; i < notUnderstoodHeaders.Count; ++i)
            {
                headers.Add(new NotUnderstoodHeader(notUnderstoodHeaders[i].Name, notUnderstoodHeaders[i].Namespace));
            }
        }

        class NotUnderstoodHeader : MessageHeader
        {
            string notUnderstoodName;
            string notUnderstoodNs;

            public NotUnderstoodHeader(string name, string ns)
            {
                notUnderstoodName = name;
                notUnderstoodNs = ns;
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
                writer.WriteXmlnsAttribute(null, notUnderstoodNs);
                writer.WriteStartAttribute(Message12Strings.QName);
                writer.WriteQualifiedName(notUnderstoodName, notUnderstoodNs);
                writer.WriteEndAttribute();
            }

            protected override void OnWriteHeaderContents(XmlDictionaryWriter writer, MessageVersion messageVersion)
            {
                // empty
            }
        }
    }

}