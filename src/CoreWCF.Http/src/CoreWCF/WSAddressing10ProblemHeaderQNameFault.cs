// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Xml;
using CoreWCF.Channels;

namespace CoreWCF
{
    internal class WSAddressing10ProblemHeaderQNameFault : MessageFault
    {
        private readonly FaultCode _code;
        private readonly FaultReason _reason;
        private readonly string _actor;
        private readonly string _node;
        private readonly string _invalidHeaderName;

        public WSAddressing10ProblemHeaderQNameFault(MessageHeaderException e)
        {
            _invalidHeaderName = e.HeaderName;

            if (e.IsDuplicate)
            {
                _code = FaultCode.CreateSenderFaultCode(
                    new FaultCode(Addressing10Strings.InvalidAddressingHeader,
                                  AddressingVersion.WSAddressing10.Namespace,
                                  new FaultCode(Addressing10Strings.InvalidCardinality,
                                                AddressingVersion.WSAddressing10.Namespace)));
            }
            else
            {
                _code = FaultCode.CreateSenderFaultCode(
                    new FaultCode(Addressing10Strings.MessageAddressingHeaderRequired,
                                  AddressingVersion.WSAddressing10.Namespace));
            }

            _reason = new FaultReason(new FaultReasonText(e.Message, CultureInfo.CurrentCulture.Name));
            _actor = "";
            _node = "";
        }

        public WSAddressing10ProblemHeaderQNameFault(ActionMismatchAddressingException e)
        {
            _invalidHeaderName = AddressingStrings.Action;
            _code = FaultCode.CreateSenderFaultCode(
                new FaultCode(Addressing10Strings.ActionMismatch, AddressingVersion.WSAddressing10.Namespace));
            _reason = new FaultReason(new FaultReasonText(e.Message, CultureInfo.CurrentCulture.Name));
            _actor = "";
            _node = "";
        }

        public override string Actor
        {
            get
            {
                return _actor;
            }
        }

        public override FaultCode Code
        {
            get
            {
                return _code;
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
                return _node;
            }
        }

        public override FaultReason Reason
        {
            get
            {
                return _reason;
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
            writer.WriteQualifiedName(_invalidHeaderName, AddressingVersion.WSAddressing10.Namespace);
            writer.WriteEndElement();
        }

        public void AddHeaders(MessageHeaders headers)
        {
            if (headers.MessageVersion.Envelope == EnvelopeVersion.Soap11)
            {
                headers.Add(new WSAddressing10ProblemHeaderQNameHeader(_invalidHeaderName));
            }
        }

        private class WSAddressing10ProblemHeaderQNameHeader : MessageHeader
        {
            private readonly string _invalidHeaderName;

            public WSAddressing10ProblemHeaderQNameHeader(string invalidHeaderName)
            {
                _invalidHeaderName = invalidHeaderName;
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
                writer.WriteQualifiedName(_invalidHeaderName, Namespace);
                writer.WriteEndElement();
            }
        }
    }
}
