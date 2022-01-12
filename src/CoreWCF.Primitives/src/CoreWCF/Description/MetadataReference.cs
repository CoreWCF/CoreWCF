// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml;
using System.Xml.Serialization;
using CoreWCF.Channels;

namespace CoreWCF.Description
{
    [XmlRoot(ElementName = MetadataStrings.MetadataExchangeStrings.MetadataReference, Namespace = MetadataStrings.MetadataExchangeStrings.Namespace)]
    public class MetadataReference : IXmlSerializable
    {
        private AddressingVersion _addressVersion;

        public MetadataReference()
        {
        }

        public MetadataReference(EndpointAddress address, AddressingVersion addressVersion)
        {
            Address = address;
            _addressVersion = addressVersion;
        }

        public EndpointAddress Address { get; set; }

        public AddressingVersion AddressVersion
        {
            get { return _addressVersion; }
            set { _addressVersion = value; }
        }

        System.Xml.Schema.XmlSchema IXmlSerializable.GetSchema()
        {
            return null;
        }

        void IXmlSerializable.ReadXml(XmlReader reader)
        {
            Address = EndpointAddress.ReadFrom(XmlDictionaryReader.CreateDictionaryReader(reader), out _addressVersion);
        }

        void IXmlSerializable.WriteXml(XmlWriter writer)
        {
            if (Address != null)
            {
                Address.WriteContentsTo(_addressVersion, writer);
            }
        }
    }
}
