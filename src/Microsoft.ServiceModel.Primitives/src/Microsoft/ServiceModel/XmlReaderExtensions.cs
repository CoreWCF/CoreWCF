using System;
using System.Xml;

namespace Microsoft.ServiceModel
{

    internal static class XmlReaderExtensions
    {
        internal static string ReadElementString(this XmlReader reader)
        {
            if (reader.MoveToContent() != XmlNodeType.Element)
            {
                var lineInfo = reader as IXmlLineInfo;
                throw new XmlException(SR.Format(SR.Xml_InvalidNodeType, reader.NodeType.ToString()), null, lineInfo?.LineNumber ?? 0, lineInfo?.LinePosition ?? 0);
            }

            return reader.ReadElementContentAsString();
        }

        internal static string ReadElementString(this XmlReader reader, string localname, string ns)
        {
            if (reader.MoveToContent() != XmlNodeType.Element)
            {
                var lineInfo = reader as IXmlLineInfo;
                throw new XmlException(SR.Format(SR.Xml_InvalidNodeType, reader.NodeType.ToString()), null, lineInfo?.LineNumber ?? 0, lineInfo?.LinePosition ?? 0);
            }

            return reader.ReadElementContentAsString(localname, ns);
        }
    }
}