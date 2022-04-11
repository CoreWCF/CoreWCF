// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Configuration;
using System.Xml;

namespace CoreWCF.Configuration
{
    [ConfigurationCollection(typeof(XmlElementElement), AddItemName = ConfigurationStrings.XmlElement, CollectionType = ConfigurationElementCollectionType.BasicMap)]
    public sealed class XmlElementElementCollection : ServiceModelConfigurationElementCollection<XmlElementElement>
    {
        public XmlElementElementCollection()
            : base(ConfigurationElementCollectionType.BasicMap, ConfigurationStrings.XmlElement) { }

        protected override object GetElementKey(ConfigurationElement element)
        {
            if (element == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(element));
            }

            return ((XmlElementElement)element).XmlElement.OuterXml;
        }

        protected override void Unmerge(ConfigurationElement sourceElement,
                                        ConfigurationElement parentElement,
                                        ConfigurationSaveMode saveMode)
        {
            if (sourceElement != null)
            {
                // Just copy from parent to here-- 
                XmlElementElementCollection source = (XmlElementElementCollection)sourceElement;
                XmlElementElementCollection parent = (XmlElementElementCollection)parentElement;
                for (int i = 0; i < source.Count; ++i)
                {
                    XmlElementElement element = (XmlElementElement)source.BaseGet(i);
                    if (parent?.BaseGet(GetElementKey(element)) == null)
                    {
                        XmlElementElement xmlElement = new XmlElementElement();
                        xmlElement.ResetInternal(element);
                        BaseAdd(xmlElement);
                    }
                }
            }
        }

        protected override bool OnDeserializeUnrecognizedElement(string elementName, XmlReader reader)
        {
            XmlDocument doc = new XmlDocument();
            BaseAdd(new XmlElementElement((XmlElement)doc.ReadNode(reader)));
            return true;
        }
    }
}
