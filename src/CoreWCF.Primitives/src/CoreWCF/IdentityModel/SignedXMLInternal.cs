using System;
using System.Collections.Generic;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace CoreWCF.IdentityModel
{
    /// <summary>
    /// Internal class to override GetIdElement
    /// </summary>
    class SignedXMLInternal : SignedXml
    {

        public SignedXMLInternal(): base()
        {

        }
        public SignedXMLInternal(XmlDocument document):base(document)
        {

        }
        public SignedXMLInternal(XmlElement elem) : base(elem)
        {

        }
        public override XmlElement GetIdElement(XmlDocument document, string idValue)
        {
            
            XmlElement element =  base.GetIdElement(document, idValue);
            if(element == null)
            {
                element = GetElementByIdInternal(document, idValue);
            }
            return element;
        }

        private XmlElement GetElementByIdInternal(XmlDocument document, String idValue)
        {
            XElement xElement = XDocument.Load(document.CreateNavigator().ReadSubtree()).Root;
            xElement = FindXlement(xElement, idValue);
            if (xElement == null)
                return null;
            var doc = new XmlDocument();
            using (XmlReader reader = xElement.CreateReader())
            {
                doc.Load(reader);
            }
            return doc.DocumentElement;

        }
        private XElement FindXlement(XElement element, String idValue)
        {
            var attributes = element.Attributes();
            foreach (var attr in attributes)
            {
                if (String.Compare(attr.Name.LocalName, "id", true) == 0 
                    && String.Compare(attr.Value, idValue,true)==0 )
                {
                    return element;
                }
            };
            XElement finalResult = null;
            foreach (var child in element.Descendants())
            {
                finalResult = FindXlement(child, idValue);
                if (finalResult != null)
                    return finalResult;
            }
            return finalResult;
        }
    }
}
