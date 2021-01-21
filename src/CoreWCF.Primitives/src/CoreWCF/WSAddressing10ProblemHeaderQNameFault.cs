// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Xml;
using CoreWCF.Channels;

namespace CoreWCF
{
    internal class WSAddressing10ProblemHeaderQNameFault : MessageFault
    {
        private readonly FaultCode code;
        private readonly FaultReason reason;
        private readonly string actor;
        private readonly string node;
        private readonly string invalidHeaderName;

        public WSAddressing10ProblemHeaderQNameFault(MessageHeaderException e)
        {
            invalidHeaderName = e.HeaderName;

            if (e.IsDuplicate)
            {
                code = FaultCode.CreateSenderFaultCode(
                    new FaultCode(Addressing10Strings.InvalidAddressingHeader,
                                  AddressingVersion.WSAddressing10.Namespace,
                                  new FaultCode(Addressing10Strings.InvalidCardinality,
                                                AddressingVersion.WSAddressing10.Namespace)));
            }
            else
            {
                code = FaultCode.CreateSenderFaultCode(
                    new FaultCode(Addressing10Strings.MessageAddressingHeaderRequired,
                                  AddressingVersion.WSAddressing10.Namespace));
            }

            reason = new FaultReason(e.Message, CultureInfo.CurrentCulture);
            actor = "";
            node = "";
        }

        public WSAddressing10ProblemHeaderQNameFault(ActionMismatchAddressingException e)
        {
            invalidHeaderName = AddressingStrings.Action;
            code = FaultCode.CreateSenderFaultCode(
                new FaultCode(Addressing10Strings.ActionMismatch, AddressingVersion.WSAddressing10.Namespace));
            reason = new FaultReason(e.Message, CultureInfo.CurrentCulture);
            actor = "";
            node = "";
        }

        public override string Actor
        {
            get
            {
                return actor;
            }
        }

        public override FaultCode Code
        {
            get
            {
                return code;
            }
        }

        public override bool HasDetail
        {
            get
            {
                return true;
            }
        }

        public override string Node
        {
            get
            {
                return node;
            }
        }

        public override FaultReason Reason
        {
            get
            {
                return reason;
            }
        }

        protected override void OnWriteDetail(XmlDictionaryWriter writer, EnvelopeVersion version)
        {
            if (version == EnvelopeVersion.Soap12)  // Soap11 wants the detail in the header
            {
                OnWriteStartDetail(writer, version);
                OnWriteDetailContents(writer);
                writer.WriteEndElement();
            }
        }

        protected override void OnWriteDetailContents(XmlDictionaryWriter writer)
        {
            writer.WriteStartElement(Addressing10Strings.ProblemHeaderQName, AddressingVersion.WSAddressing10.Namespace);
            writer.WriteQualifiedName(invalidHeaderName, AddressingVersion.WSAddressing10.Namespace);
            writer.WriteEndElement();
        }

        public void AddHeaders(MessageHeaders headers)
        {
            if (headers.MessageVersion.Envelope == EnvelopeVersion.Soap11)
            {
                headers.Add(new WSAddressing10ProblemHeaderQNameHeader(invalidHeaderName));
            }
        }

        private class WSAddressing10ProblemHeaderQNameHeader : MessageHeader
        {
            private readonly string invalidHeaderName;

            public WSAddressing10ProblemHeaderQNameHeader(string invalidHeaderName)
            {
                this.invalidHeaderName = invalidHeaderName;
            }

            public override string Name
            {
                get { return Addressing10Strings.FaultDetail; }
            }

            public override string Namespace
            {
                get { return AddressingVersion.WSAddressing10.Namespace; }
            }

            protected override void OnWriteStartHeader(XmlDictionaryWriter writer, MessageVersion messageVersion)
            {
                writer.WriteStartElement(Name, Namespace);
            }

            protected override void OnWriteHeaderContents(XmlDictionaryWriter writer, MessageVersion messageVersion)
            {
                writer.WriteStartElement(Addressing10Strings.ProblemHeaderQName, Namespace);
                writer.WriteQualifiedName(invalidHeaderName, Namespace);
                writer.WriteEndElement();
            }
        }
    }

}