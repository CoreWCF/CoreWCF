// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Configuration;
using System.Xml;
using CoreWCF.Channels;

namespace CoreWCF.Configuration
{
    public sealed class AddressHeaderCollectionElement : ServiceModelConfigurationElement
    {
        public AddressHeaderCollectionElement()
        {
        }

        internal void Copy(AddressHeaderCollectionElement source)
        {
            if (source == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(source));
            }

            PropertyInformationCollection properties = source.ElementInformation.Properties;
            if (properties[ConfigurationStrings.Headers].ValueOrigin != PropertyValueOrigin.Default)
            {
                Headers = source.Headers;
            }
        }

        [ConfigurationProperty(ConfigurationStrings.Headers, DefaultValue = null)]
        public AddressHeaderCollection Headers
        {
            get
            {
                AddressHeaderCollection retVal = (AddressHeaderCollection)base[ConfigurationStrings.Headers];
                if (null == retVal)
                {
                    retVal = new AddressHeaderCollection();
                }
                return retVal;
            }
            set
            {
                if (value == null)
                {
                    value = new AddressHeaderCollection();
                }
                base[ConfigurationStrings.Headers] = value;
            }
        }


        protected override void DeserializeElement(XmlReader reader, bool serializeCollectionKey)
        {
            DeserializeElementCore(XmlDictionaryReader.CreateDictionaryReader(reader));
        }

        private void DeserializeElementCore(XmlDictionaryReader reader)
        {
            Headers = new AddressHeaderCollection(new[] { new XmlElementBackedAddressHeader(reader) });
        }

        protected override bool SerializeToXmlElement(XmlWriter writer, string elementName)
        {
            bool dataToWrite = Headers.Count != 0;
            if (dataToWrite && writer != null)
            {
                writer.WriteStartElement(elementName);

                XmlDictionaryWriter dictionaryWriter = XmlDictionaryWriter.CreateDictionaryWriter(writer);
                foreach (AddressHeader header in Headers)
                {
                    header.WriteAddressHeader(dictionaryWriter);
                }

                writer.WriteEndElement();
            }
            return dataToWrite;
        }

        internal void InitializeFrom(AddressHeaderCollection headers)
        {
            SetPropertyValueIfNotDefaultValue(ConfigurationStrings.Headers, headers);
        }
    }
}
