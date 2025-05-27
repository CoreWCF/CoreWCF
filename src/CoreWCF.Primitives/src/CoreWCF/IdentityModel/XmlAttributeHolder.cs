// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Xml;

namespace CoreWCF.IdentityModel
{
    internal struct XmlAttributeHolder
    {
        public static XmlAttributeHolder[] emptyArray = Array.Empty<XmlAttributeHolder>();

        public XmlAttributeHolder(string prefix, string localName, string ns, string value)
        {
            Prefix = prefix;
            LocalName = localName;
            NamespaceUri = ns;
            Value = value;
        }

        public string Prefix { get; }

        public string NamespaceUri { get; }

        public string LocalName { get; }

        public string Value { get; }

        public void WriteTo(XmlWriter writer)
        {
            writer.WriteStartAttribute(Prefix, LocalName, NamespaceUri);
            writer.WriteString(Value);
            writer.WriteEndAttribute();
        }

        public static void WriteAttributes(XmlAttributeHolder[] attributes, XmlWriter writer)
        {
            for (int i = 0; i < attributes.Length; i++)
            {
                attributes[i].WriteTo(writer);
            }
        }

        public static XmlAttributeHolder[] ReadAttributes(XmlDictionaryReader reader)
        {
            int maxSizeOfHeaders = int.MaxValue;
            return ReadAttributes(reader, ref maxSizeOfHeaders);
        }

        public static XmlAttributeHolder[] ReadAttributes(XmlDictionaryReader reader, ref int maxSizeOfHeaders)
        {
            if (reader.AttributeCount == 0)
            {
                return emptyArray;
            }

            XmlAttributeHolder[] attributes = new XmlAttributeHolder[reader.AttributeCount];
            reader.MoveToFirstAttribute();
            for (int i = 0; i < attributes.Length; i++)
            {
                string ns = reader.NamespaceURI;
                string localName = reader.LocalName;
                string prefix = reader.Prefix;
                string value = string.Empty;
                while (reader.ReadAttributeValue())
                {
                    if (value.Length == 0)
                    {
                        value = reader.Value;
                    }
                    else
                    {
                        value += reader.Value;
                    }
                }
                Deduct(prefix, ref maxSizeOfHeaders);
                Deduct(localName, ref maxSizeOfHeaders);
                Deduct(ns, ref maxSizeOfHeaders);
                Deduct(value, ref maxSizeOfHeaders);
                attributes[i] = new XmlAttributeHolder(prefix, localName, ns, value);
                reader.MoveToNextAttribute();
            }
            reader.MoveToElement();
            return attributes;
        }

        private static void Deduct(string s, ref int maxSizeOfHeaders)
        {
            int byteCount = s.Length * sizeof(char);
            if (byteCount > maxSizeOfHeaders)
            {
                throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SRCommon.XmlBufferQuotaExceeded));
            }
            maxSizeOfHeaders -= byteCount;
        }

        public static string GetAttribute(XmlAttributeHolder[] attributes, string localName, string ns)
        {
            for (int i = 0; i < attributes.Length; i++)
            {
                if (attributes[i].LocalName == localName && attributes[i].NamespaceUri == ns)
                {
                    return attributes[i].Value;
                }
            }

            return null;
        }
    }
}
