// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Configuration;
using System.Security;
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
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("source");
            }

            PropertyInformationCollection properties = source.ElementInformation.Properties;
            if (properties[ConfigurationStrings.Headers].ValueOrigin != PropertyValueOrigin.Default)
            {
                this.Headers = source.Headers;
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

        [SecuritySafeCritical]
        protected override void DeserializeElement(XmlReader reader, bool serializeCollectionKey)
        {
            DeserializeElementCore(reader);
        }

        private void DeserializeElementCore(XmlReader reader)
        {
            //TODO Find proper way to handle this this.Headers = AddressHeaderCollection.ReadServiceParameters(XmlDictionaryReader.CreateDictionaryReader(reader));
        }

       protected override bool SerializeToXmlElement(XmlWriter writer, string elementName)
        {
            bool dataToWrite = this.Headers.Count != 0;
            if (dataToWrite && writer != null)
            {
                writer.WriteStartElement(elementName);
                //TODO Find proper way to handle this this.Headers.WriteContentsTo(XmlDictionaryWriter.CreateDictionaryWriter(writer));
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
