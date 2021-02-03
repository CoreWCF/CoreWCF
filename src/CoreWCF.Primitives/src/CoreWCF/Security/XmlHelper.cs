// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;
using System.Xml;

namespace CoreWCF.Security
{
    internal static class XmlHelper
    {
        internal static void AddNamespaceDeclaration(XmlDictionaryWriter writer, string prefix, XmlDictionaryString ns)
        {
            string p = writer.LookupPrefix(ns.Value);
            if (p == null || p != prefix)
            {
                writer.WriteXmlnsAttribute(prefix, ns);
            }
        }

        internal static string EnsureNamespaceDefined(XmlDictionaryWriter writer, XmlDictionaryString ns, string defaultPrefix)
        {
            string p = writer.LookupPrefix(ns.Value);
            if (p == null)
            {
                writer.WriteXmlnsAttribute(defaultPrefix, ns);
                p = defaultPrefix;
            }

            return p;
        }

        internal static XmlQualifiedName GetAttributeValueAsQName(XmlReader reader, string attributeName)
        {
            string qname = reader.GetAttribute(attributeName);
            if (qname == null)
            {
                return null;
            }
            return GetValueAsQName(reader, qname);
        }

        /// <summary>
        /// Enforces that parent has exactly 1 child of type XML element and nothing else (barring comments and whitespaces)
        /// and returns the child
        /// </summary>
        internal static XmlElement GetChildElement(XmlElement parent)
        {
            if (parent == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(parent));
            }

            XmlElement result = null;
            for (int i = 0; i < parent.ChildNodes.Count; ++i)
            {
                XmlNode child = parent.ChildNodes[i];
                if (child.NodeType == XmlNodeType.Whitespace || child.NodeType == XmlNodeType.Comment)
                {
                    continue;
                }
                else if (child.NodeType == XmlNodeType.Element && result == null)
                {
                    result = ((XmlElement)child);
                }
                else
                {
                    OnUnexpectedChildNodeError(parent, child);
                }
            }

            if (result == null)
            {
                OnChildNodeTypeMissing(parent, XmlNodeType.Element);
            }

            return result;
        }

        internal static XmlElement GetChildElement(XmlElement parent, string childLocalName, string childNamespace)
        {
            if (parent == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(parent));
            }

            for (int i = 0; i < parent.ChildNodes.Count; ++i)
            {
                XmlNode child = parent.ChildNodes[i];

                if (child.NodeType == XmlNodeType.Whitespace || child.NodeType == XmlNodeType.Comment)
                {
                    continue;
                }
                else if (child.NodeType == XmlNodeType.Element)
                {
                    if (child.LocalName == childLocalName && child.NamespaceURI == childNamespace)
                    {
                        return ((XmlElement)child);
                    }
                }
                else
                {
                    OnUnexpectedChildNodeError(parent, child);
                }
            }

            return null;
        }

        internal static XmlQualifiedName GetValueAsQName(XmlReader reader, string value)
        {
            SplitIntoPrefixAndName(value, out string prefix, out string name);
            string ns = reader.LookupNamespace(prefix);
            if (ns == null && prefix.Length > 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.CouldNotFindNamespaceForPrefix, prefix)));
            }
            return new XmlQualifiedName(name, ns);
        }

        internal static string GetWhiteSpace(XmlReader reader)
        {
            string s = null;
            StringBuilder sb = null;
            while (reader.NodeType == XmlNodeType.Whitespace || reader.NodeType == XmlNodeType.SignificantWhitespace)
            {
                if (sb != null)
                {
                    sb.Append(reader.Value);
                }
                else if (s != null)
                {
                    sb = new StringBuilder(s);
                    sb.Append(reader.Value);
                    s = null;
                }
                else
                {
                    s = reader.Value;
                }
                if (!reader.Read())
                {
                    break;
                }
            }
            return sb != null ? sb.ToString() : s;
        }

        internal static bool IsWhitespaceOrComment(XmlReader reader)
        {
            if (reader.NodeType == XmlNodeType.Comment)
            {
                return true;
            }
            else if (reader.NodeType == XmlNodeType.Whitespace)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        internal static void OnChildNodeTypeMissing(string parentName, XmlNodeType expectedNodeType)
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.ChildNodeTypeMissing, parentName, expectedNodeType)));
        }

        internal static void OnChildNodeTypeMissing(XmlElement parent, XmlNodeType expectedNodeType)
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.ChildNodeTypeMissing, parent.Name, expectedNodeType)));
        }

        internal static void OnEmptyElementError(XmlReader r)
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.EmptyXmlElementError, r.Name)));
        }

        internal static void OnEmptyElementError(XmlElement e)
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.EmptyXmlElementError, e.Name)));
        }

        internal static void OnEOF()
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.UnexpectedEndOfFile)));
        }

        internal static void OnNamespaceMissing(string prefix)
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.CouldNotFindNamespaceForPrefix, prefix)));
        }

        internal static void OnRequiredAttributeMissing(string attrName, string elementName)
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.RequiredAttributeMissing, attrName, elementName)));
        }

        internal static void OnRequiredElementMissing(string elementName, string elementNamespace)
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.ExpectedElementMissing, elementName, elementNamespace)));
        }

        internal static void OnUnexpectedChildNodeError(string parentName, XmlReader r)
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.UnexpectedXmlChildNode, r.Name, r.NodeType, parentName)));
        }

        internal static void OnUnexpectedChildNodeError(XmlElement parent, XmlNode n)
        {
            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new XmlException(SR.Format(SR.UnexpectedXmlChildNode, n.Name, n.NodeType, parent.Name)));
        }

        internal static string ReadEmptyElementAndRequiredAttribute(XmlDictionaryReader reader,
            XmlDictionaryString name, XmlDictionaryString namespaceUri, XmlDictionaryString attributeName,
            out string prefix)
        {
            reader.MoveToStartElement(name, namespaceUri);
            prefix = reader.Prefix;
            bool isEmptyElement = reader.IsEmptyElement;
            string value = reader.GetAttribute(attributeName, null);
            if (value == null)
            {
                OnRequiredAttributeMissing(attributeName.Value, null);
            }
            reader.Read();

            if (!isEmptyElement)
            {
                reader.ReadEndElement();
            }
            return value;
        }

        internal static string GetRequiredNonEmptyAttribute(XmlDictionaryReader reader, XmlDictionaryString name, XmlDictionaryString ns)
        {
            string value = reader.GetAttribute(name, ns);
            if (value == null || value.Length == 0)
            {
                OnRequiredAttributeMissing(name.Value, reader?.Name);
            }
            return value;
        }

        internal static byte[] GetRequiredBase64Attribute(XmlDictionaryReader reader, XmlDictionaryString name, XmlDictionaryString ns)
        {
            if (!reader.MoveToAttribute(name.Value, ns?.Value))
            {
                OnRequiredAttributeMissing(name.Value, ns?.Value);
            }
            byte[] value = reader.ReadContentAsBase64();
            if (value == null || value.Length == 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                    new XmlException(SR.Format(SR.EmptyBase64Attribute, name, ns)));
            }

            return value;
        }

        internal static string ReadTextElementAsTrimmedString(XmlElement element)
        {
            if (element == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgumentNull(nameof(element));
            }

            using (XmlReader reader = new XmlNodeReader(element))
            {
                reader.MoveToContent();
                return XmlUtil.Trim(reader.ReadElementContentAsString());
            }
        }

        internal static void SplitIntoPrefixAndName(string qName, out string prefix, out string name)
        {
            string[] parts = qName.Split(':');
            if (parts.Length > 2)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperArgument(SR.Format(SR.InvalidQName));
            }

            if (parts.Length == 2)
            {
                prefix = parts[0].Trim();
                name = parts[1].Trim();
            }
            else
            {
                prefix = string.Empty;
                name = qName.Trim();
            }
        }

        internal static void ValidateIdPrefix(string idPrefix)
        {
            if (idPrefix == null)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentNullException(nameof(idPrefix)));
            }

            if (idPrefix.Length == 0)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(idPrefix), SR.ValueMustBeGreaterThanZero));
            }

            if ((!char.IsLetter(idPrefix[0]) && idPrefix[0] != '_'))
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(idPrefix), SR.Format(SR.InValidateIdPrefix, idPrefix[0])));
            }

            for (int i = 1; i < idPrefix.Length; i++)
            {
                char c = idPrefix[i];
                if (!char.IsLetter(c) && !char.IsNumber(c) && c != '.' && c != '_' && c != '-')
                {
                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ArgumentOutOfRangeException(nameof(idPrefix), SR.Format(SR.InValidateId, idPrefix[i])));
                }
            }
        }

        internal static UniqueId GetAttributeAsUniqueId(XmlDictionaryReader reader, XmlDictionaryString localName, XmlDictionaryString ns)
        {
            return GetAttributeAsUniqueId(reader, localName.Value, (ns?.Value));
        }

        private static UniqueId GetAttributeAsUniqueId(XmlDictionaryReader reader, string name, string ns)
        {
            if (!reader.MoveToAttribute(name, ns))
            {
                return null;
            }

            UniqueId id = reader.ReadContentAsUniqueId();
            reader.MoveToElement();

            return id;
        }

        public static void WriteAttributeStringAsUniqueId(XmlDictionaryWriter writer, string prefix, XmlDictionaryString localName, XmlDictionaryString ns, UniqueId id)
        {
            writer.WriteStartAttribute(prefix, localName, ns);
            writer.WriteValue(id);
            writer.WriteEndAttribute();
        }

        public static void WriteElementStringAsUniqueId(XmlWriter writer, string localName, UniqueId id)
        {
            writer.WriteStartElement(localName);
            writer.WriteValue(id);
            writer.WriteEndElement();
        }
        public static void WriteElementStringAsUniqueId(XmlDictionaryWriter writer, XmlDictionaryString localName, XmlDictionaryString ns, UniqueId id)
        {
            writer.WriteStartElement(localName, ns);
            writer.WriteValue(id);
            writer.WriteEndElement();
        }

        public static void WriteElementContentAsInt64(XmlDictionaryWriter writer, XmlDictionaryString localName, XmlDictionaryString ns, long value)
        {
            writer.WriteStartElement(localName, ns);
            writer.WriteValue(value);
            writer.WriteEndElement();
        }

        public static long ReadElementContentAsInt64(XmlDictionaryReader reader)
        {
            reader.ReadFullStartElement();
            long i = reader.ReadContentAsLong();
            reader.ReadEndElement();
            return i;
        }

        public static void WriteStringAsUniqueId(XmlDictionaryWriter writer, UniqueId id)
        {
            writer.WriteValue(id);
        }

        public static UniqueId ReadElementStringAsUniqueId(XmlDictionaryReader reader, XmlDictionaryString localName, XmlDictionaryString ns)
        {
            if (reader.IsStartElement(localName, ns) && reader.IsEmptyElement)
            {
                reader.Read();
                return new UniqueId(string.Empty);
            }

            reader.ReadStartElement(localName, ns);
            UniqueId id = reader.ReadContentAsUniqueId();
            reader.ReadEndElement();
            return id;
        }

        public static UniqueId ReadElementStringAsUniqueId(XmlDictionaryReader reader)
        {
            if (reader.IsStartElement() && reader.IsEmptyElement)
            {
                reader.Read();
                return new UniqueId(string.Empty);
            }

            reader.ReadStartElement();
            UniqueId id = reader.ReadContentAsUniqueId();
            reader.ReadEndElement();
            return id;
        }

        public static UniqueId ReadTextElementAsUniqueId(XmlElement element)
        {
            return new UniqueId(ReadTextElementAsTrimmedString(element));
        }
    }
}
