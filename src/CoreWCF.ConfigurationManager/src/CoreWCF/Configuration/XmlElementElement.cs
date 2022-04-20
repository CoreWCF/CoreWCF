// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Configuration;
using System.Xml;

namespace CoreWCF.Configuration
{
    public sealed class XmlElementElement : ConfigurationElement
    {
        public XmlElementElement()
        {
        }

        public XmlElementElement(XmlElement element) : this()
        {
            XmlElement = element;
        }

        public void Copy(XmlElementElement source)
        {
            if (IsReadOnly())
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ConfigurationErrorsException(SR.Format(SR.ConfigReadOnly)));
            }
            if (null == source)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(source));
            }

            if (null != source.XmlElement)
            {
                XmlElement = (XmlElement)source.XmlElement.Clone();
            }
        }

        protected override void DeserializeElement(XmlReader reader, bool serializeCollectionKey)
        {
            DeserializeElementCore(reader);
        }

        private void DeserializeElementCore(XmlReader reader)
        {
            XmlDocument doc = new XmlDocument();
            XmlElement = (XmlElement)doc.ReadNode(reader);
        }

        internal void ResetInternal(XmlElementElement element)
        {
            Reset(element);
        }

        protected override bool SerializeToXmlElement(XmlWriter writer, string elementName)
        {
            bool dataToWrite = XmlElement != null;
            if (dataToWrite && writer != null)
            {
                if (!string.Equals(elementName, ConfigurationStrings.XmlElement, StringComparison.Ordinal))
                {
                    writer.WriteStartElement(elementName);
                }

                using (XmlNodeReader reader = new XmlNodeReader(XmlElement))
                {
                    writer.WriteNode(reader, false);
                }

                if (!string.Equals(elementName, ConfigurationStrings.XmlElement, StringComparison.Ordinal))
                {
                    writer.WriteEndElement();
                }
            }
            return dataToWrite;
        }

        protected override void PostDeserialize()
        {
            Validate();
            base.PostDeserialize();
        }

        private void Validate()
        {
            if (XmlElement == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ConfigurationErrorsException(SR.Format(SR.ConfigXmlElementMustBeSet),
                    ElementInformation.Source,
                    ElementInformation.LineNumber));
            }
        }

        [ConfigurationProperty(ConfigurationStrings.XmlElement, DefaultValue = null, Options = ConfigurationPropertyOptions.IsKey)]
        public XmlElement XmlElement
        {
            get { return (XmlElement)base[ConfigurationStrings.XmlElement]; }
            set { base[ConfigurationStrings.XmlElement] = value; }
        }
    }
}
