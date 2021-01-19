using System;
using System.Collections.Generic;
using System.Security.Cryptography;
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
        public SignedXMLInternal() { }

        public SignedXMLInternal(XmlDocument document) : base(document) { }

        public SignedXMLInternal(XmlElement elem) : base(elem) { }

        public override XmlElement GetIdElement(XmlDocument document, string idValue)
        {
            // The default GetIdElement implementation can't find Id attributes which have a namespace specified
            // Only trying the base implementation as a last ditch effort.
            return GetSingleReferenceTarget(document, "Id", idValue) ??
                   GetSingleReferenceTarget(document, "id", idValue) ??
                   GetSingleReferenceTarget(document, "ID", idValue) ??
                   base.GetIdElement(document, idValue);
        }

        private static XmlElement GetSingleReferenceTarget(XmlDocument document, string idAttributeName, string idValue)
        {
            // XmlDocument.GetElementById only works for elements specified in a DTD to have an IDREF.
            // As we don't use DTD's in SOAP, that method is ineffective.
            string xPath = "//*[@*[local-name()='" + idAttributeName + "']='" + idValue + "']";

            // http://www.w3.org/TR/xmldsig-core/#sec-ReferenceProcessingModel says that for the form URI="#chapter1":
            //
            //   Identifies a node-set containing the element with ID attribute value 'chapter1' ...
            //
            // Note that it uses the singular. Therefore, if the match is ambiguous, we should consider the document invalid.
            //
            // In this case, we'll treat it the same as having found nothing across all fallbacks (but shortcut so that we don't
            // fall into a trap of finding a secondary element which wasn't the originally signed one).

            XmlNodeList nodeList = document.SelectNodes(xPath);

            if (nodeList == null || nodeList.Count == 0) { return null; }
            if (nodeList.Count == 1) { return nodeList[0] as XmlElement; }

            //throw new CryptographicException(SR.Cryptography_Xml_InvalidReference);
            throw new CryptographicException("Cryptography_Xml_InvalidReference");
        }
    }
}
