// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;

namespace CoreWCF.Security
{
    internal sealed class BinaryNegotiation
    {
        private readonly byte[] negotiationData;
        private XmlDictionaryString valueTypeUriDictionaryString;

        public BinaryNegotiation(
            string valueTypeUri,
            byte[] negotiationData)
        {
            if (valueTypeUri == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(valueTypeUri));
            }
            if (negotiationData == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(negotiationData));
            }
            valueTypeUriDictionaryString = null;
            ValueTypeUri = valueTypeUri;
            this.negotiationData = negotiationData;
        }

        public BinaryNegotiation(
            XmlDictionaryString valueTypeDictionaryString,
            byte[] negotiationData)
        {
            if (valueTypeDictionaryString == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(valueTypeDictionaryString));
            }
            if (negotiationData == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(negotiationData));
            }
            valueTypeUriDictionaryString = valueTypeDictionaryString;
            ValueTypeUri = valueTypeDictionaryString.Value;
            this.negotiationData = negotiationData;
        }

        public void Validate(XmlDictionaryString valueTypeUriDictionaryString)
        {
            if (ValueTypeUri != valueTypeUriDictionaryString.Value)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new SecurityNegotiationException(SR.Format(SR.IncorrectBinaryNegotiationValueType, ValueTypeUri)));
            }
            this.valueTypeUriDictionaryString = valueTypeUriDictionaryString;
        }

        public void WriteTo(XmlDictionaryWriter writer, string prefix, XmlDictionaryString localName, XmlDictionaryString ns, XmlDictionaryString valueTypeLocalName, XmlDictionaryString valueTypeNs)
        {
            writer.WriteStartElement(prefix, localName, ns);
            writer.WriteStartAttribute(valueTypeLocalName, valueTypeNs);
            if (valueTypeUriDictionaryString != null)
            {
                writer.WriteString(valueTypeUriDictionaryString);
            }
            else
            {
                writer.WriteString(ValueTypeUri);
            }

            writer.WriteEndAttribute();
            writer.WriteStartAttribute(XD.SecurityJan2004Dictionary.EncodingType, null);
            writer.WriteString(XD.SecurityJan2004Dictionary.EncodingTypeValueBase64Binary);
            writer.WriteEndAttribute();
            writer.WriteBase64(negotiationData, 0, negotiationData.Length);
            writer.WriteEndElement();
        }

        public string ValueTypeUri { get; }

        public byte[] GetNegotiationData()
        {
            // avoid copying since this is internal and callers use it as read-only
            return negotiationData;
        }
    }
}
