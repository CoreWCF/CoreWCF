// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;

namespace CoreWCF.Security
{
    internal sealed class BinaryNegotiation
    {
        private readonly byte[] _negotiationData;
        private XmlDictionaryString _valueTypeUriDictionaryString;

        public BinaryNegotiation(
            string valueTypeUri,
            byte[] negotiationData)
        {
            _valueTypeUriDictionaryString = null;
            ValueTypeUri = valueTypeUri ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(valueTypeUri));
            _negotiationData = negotiationData ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(negotiationData));
        }

        public BinaryNegotiation(
            XmlDictionaryString valueTypeDictionaryString,
            byte[] negotiationData)
        {
            _valueTypeUriDictionaryString = valueTypeDictionaryString ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(valueTypeDictionaryString));
            ValueTypeUri = valueTypeDictionaryString.Value;
            _negotiationData = negotiationData ?? throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(negotiationData));
        }

        public void Validate(XmlDictionaryString valueTypeUriDictionaryString)
        {
            if (ValueTypeUri != valueTypeUriDictionaryString.Value)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityNegotiationException(SR.Format(SR.IncorrectBinaryNegotiationValueType, ValueTypeUri)));
            }
            _valueTypeUriDictionaryString = valueTypeUriDictionaryString;
        }

        public void WriteTo(XmlDictionaryWriter writer, string prefix, XmlDictionaryString localName, XmlDictionaryString ns, XmlDictionaryString valueTypeLocalName, XmlDictionaryString valueTypeNs)
        {
            writer.WriteStartElement(prefix, localName, ns);
            writer.WriteStartAttribute(valueTypeLocalName, valueTypeNs);
            if (_valueTypeUriDictionaryString != null)
            {
                writer.WriteString(_valueTypeUriDictionaryString);
            }
            else
            {
                writer.WriteString(ValueTypeUri);
            }

            writer.WriteEndAttribute();
            writer.WriteStartAttribute(XD.SecurityJan2004Dictionary.EncodingType, null);
            writer.WriteString(XD.SecurityJan2004Dictionary.EncodingTypeValueBase64Binary);
            writer.WriteEndAttribute();
            writer.WriteBase64(_negotiationData, 0, _negotiationData.Length);
            writer.WriteEndElement();
        }

        public string ValueTypeUri { get; }

        public byte[] GetNegotiationData()
        {
            // avoid copying since this is internal and callers use it as read-only
            return _negotiationData;
        }
    }
}
