// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Configuration;
using System.Security;
using System.Xml;
using CoreWCF.Runtime;

namespace CoreWCF.Configuration
{
    public sealed class XmlElementElement : ConfigurationElement
    {
        public XmlElementElement()
        {
        }

        public XmlElementElement(XmlElement element) : this()
        {
            this.XmlElement = element;
        }

        public void Copy(XmlElementElement source)
        {
            if (this.IsReadOnly())
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ConfigurationErrorsException(SR.Format(SR.ConfigReadOnly)));
            }
            if (null == source)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull("source");
            }

            if (null != source.XmlElement)
            {
                this.XmlElement = (XmlElement)source.XmlElement.Clone();
            }
        }

       [SecuritySafeCritical]
        protected override void DeserializeElement(XmlReader reader, bool serializeCollectionKey)
        {
            DeserializeElementCore(reader);
        }

        private void DeserializeElementCore(XmlReader reader)
        {
            XmlDocument doc = new XmlDocument();
            this.XmlElement = (XmlElement)doc.ReadNode(reader);
        }

        internal void ResetInternal(XmlElementElement element)
        {
            this.Reset(element);
        }

        protected override bool SerializeToXmlElement(XmlWriter writer, string elementName)
        {
            bool dataToWrite = this.XmlElement != null;
            if (dataToWrite && writer != null)
            {
                if (!string.Equals(elementName, ConfigurationStrings.XmlElement, StringComparison.Ordinal))
                {
                    writer.WriteStartElement(elementName);
                }

                using (XmlNodeReader reader = new XmlNodeReader(this.XmlElement))
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
            this.Validate();
            base.PostDeserialize();
        }

        private void Validate()
        {
            if (this.XmlElement == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ConfigurationErrorsException(SR.Format(SR.ConfigXmlElementMustBeSet),
                    this.ElementInformation.Source,
                    this.ElementInformation.LineNumber));
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
