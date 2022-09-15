// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Xml;
using CoreWCF.Channels;
using CoreWCF.Runtime;

namespace CoreWCF.Dispatcher
{
    internal class EndpointAddressProcessor
    {
        internal static readonly QNameKeyComparer QNameComparer = new QNameKeyComparer();

        // QName Attributes
        //internal static readonly string XsiNs = XmlSchema.InstanceNamespace;
        internal static readonly string XsiNs = "http://www.w3.org/2001/XMLSchema-instance";
        internal const string SerNs = "http://schemas.microsoft.com/2003/10/Serialization/";
        internal const string TypeLN = "type";
        internal const string ItemTypeLN = "ItemType";
        internal const string FactoryTypeLN = "FactoryType";

        // Pooling
        private readonly StringBuilder _builder;
        private byte[] _resultData;

        internal EndpointAddressProcessor(int length)
        {
            _builder = new StringBuilder();
            _resultData = new byte[length];
        }

        internal EndpointAddressProcessor Next { get; set; }

        internal static string GetComparableForm(StringBuilder builder, XmlReader reader)
        {
            List<Attr> attrSet = new List<Attr>();
            int valueLength = -1;
            while (!reader.EOF)
            {
                XmlNodeType type = reader.MoveToContent();
                switch (type)
                {
                    case XmlNodeType.Element:
                        CompleteValue(builder, valueLength);
                        valueLength = -1;

                        builder.Append("<");
                        AppendString(builder, reader.LocalName);
                        builder.Append(":");
                        AppendString(builder, reader.NamespaceURI);
                        builder.Append(" ");

                        // Scan attributes
                        attrSet.Clear();
                        if (reader.MoveToFirstAttribute())
                        {
                            do
                            {
                                // Ignore namespaces
                                if (reader.Prefix == "xmlns" || reader.Name == "xmlns")
                                {
                                    continue;
                                }
                                if (reader.LocalName == AddressingStrings.IsReferenceParameter && reader.NamespaceURI == Addressing10Strings.Namespace)
                                {
                                    continue;  // ignore IsReferenceParameter
                                }

                                string val = reader.Value;
                                if ((reader.LocalName == TypeLN && reader.NamespaceURI == XsiNs) ||
                                    (reader.NamespaceURI == SerNs && (reader.LocalName == ItemTypeLN || reader.LocalName == FactoryTypeLN)))
                                {
                                    XmlUtil.ParseQName(reader, val, out string local, out string ns);
                                    val = local + "^" + local.Length.ToString(CultureInfo.InvariantCulture) + ":" + ns + "^" + ns.Length.ToString(CultureInfo.InvariantCulture);
                                }
                                else if (reader.LocalName == XD.UtilityDictionary.IdAttribute.Value && reader.NamespaceURI == XD.UtilityDictionary.Namespace.Value)
                                {
                                    // ignore wsu:Id attributes added by security to sign the header
                                    continue;
                                }
                                attrSet.Add(new Attr(reader.LocalName, reader.NamespaceURI, val));
                            } while (reader.MoveToNextAttribute());
                        }
                        reader.MoveToElement();

                        if (attrSet.Count > 0)
                        {
                            attrSet.Sort();
                            for (int i = 0; i < attrSet.Count; ++i)
                            {
                                Attr a = attrSet[i];

                                AppendString(builder, a._local);
                                builder.Append(":");
                                AppendString(builder, a._ns);
                                builder.Append("=\"");
                                AppendString(builder, a._val);
                                builder.Append("\" ");
                            }
                        }

                        if (reader.IsEmptyElement)
                        {
                            builder.Append("></>");  // Should be the same as an empty tag.
                        }
                        else
                        {
                            builder.Append(">");
                        }

                        break;

                    case XmlNodeType.EndElement:
                        CompleteValue(builder, valueLength);
                        valueLength = -1;
                        builder.Append("</>");
                        break;

                    // Need to escape CDATA values
                    case XmlNodeType.CDATA:
                        CompleteValue(builder, valueLength);
                        valueLength = -1;

                        builder.Append("<![CDATA[");
                        AppendString(builder, reader.Value);
                        builder.Append("]]>");
                        break;

                    case XmlNodeType.SignificantWhitespace:
                    case XmlNodeType.Text:
                        if (valueLength < 0)
                        {
                            valueLength = builder.Length;
                        }

                        builder.Append(reader.Value);
                        break;

                    default:
                        // Do nothing
                        break;
                }
                reader.Read();
            }
            return builder.ToString();
        }

        private static void AppendString(StringBuilder builder, string s)
        {
            builder.Append(s);
            builder.Append("^");
            builder.Append(s.Length.ToString(CultureInfo.InvariantCulture));
        }

        private static void CompleteValue(StringBuilder builder, int startLength)
        {
            if (startLength < 0)
            {
                return;
            }

            int len = builder.Length - startLength;
            builder.Append("^");
            builder.Append(len.ToString(CultureInfo.InvariantCulture));
        }

        internal void Clear(int length)
        {
            if (_resultData.Length == length)
            {
                Array.Clear(_resultData, 0, _resultData.Length);
            }
            else
            {
                _resultData = new byte[length];
            }
        }

        internal void ProcessHeaders(Message msg, Dictionary<QName, int> qnameLookup, Dictionary<string, HeaderBit[]> headerLookup)
        {
            string key;
            QName qname;
            MessageHeaders headers = msg.Headers;
            for (int j = 0; j < headers.Count; ++j)
            {
                qname.name = headers[j].Name;
                qname.ns = headers[j].Namespace;
                if (headers.MessageVersion.Addressing == AddressingVersion.WSAddressing10
                    && !headers[j].IsReferenceParameter)
                {
                    continue;
                }
                if (qnameLookup.ContainsKey(qname))
                {
                    _builder.Remove(0, _builder.Length);
                    XmlReader reader = headers.GetReaderAtHeader(j).ReadSubtree();
                    reader.Read();  // Needed after call to ReadSubtree
                    key = GetComparableForm(_builder, reader);

                    if (headerLookup.TryGetValue(key, out HeaderBit[] bits))
                    {
                        SetBit(bits);
                    }
                }
            }
        }

        internal void SetBit(HeaderBit[] bits)
        {
            if (bits.Length == 1)
            {
                _resultData[bits[0]._index] |= bits[0]._mask;
            }
            else
            {
                byte[] results = _resultData;
                for (int i = 0; i < bits.Length; ++i)
                {
                    if ((results[bits[i]._index] & bits[i]._mask) == 0)
                    {
                        results[bits[i]._index] |= bits[i]._mask;
                        break;
                    }
                }
            }
        }

        internal bool TestExact(byte[] exact)
        {
            Fx.Assert(_resultData.Length == exact.Length, "");

            byte[] results = _resultData;
            for (int i = 0; i < exact.Length; ++i)
            {
                if (results[i] != exact[i])
                {
                    return false;
                }
            }

            return true;
        }

        internal bool TestMask(byte[] mask)
        {
            if (mask == null)
            {
                return true;
            }

            byte[] results = _resultData;
            for (int i = 0; i < mask.Length; ++i)
            {
                if ((results[i] & mask[i]) != mask[i])
                {
                    return false;
                }
            }

            return true;
        }

        internal struct QName
        {
            internal string name;
            internal string ns;
        }

        internal class QNameKeyComparer : IComparer<QName>, IEqualityComparer<QName>
        {
            internal QNameKeyComparer()
            {
            }

            public int Compare(QName x, QName y)
            {
                int i = string.CompareOrdinal(x.name, y.name);
                if (i != 0)
                {
                    return i;
                }

                return string.CompareOrdinal(x.ns, y.ns);
            }

            public bool Equals(QName x, QName y)
            {
                int i = string.CompareOrdinal(x.name, y.name);
                if (i != 0)
                {
                    return false;
                }

                return string.CompareOrdinal(x.ns, y.ns) == 0;
            }

            public int GetHashCode(QName obj)
            {
                return obj.name.GetHashCode() ^ obj.ns.GetHashCode();
            }
        }

        internal struct HeaderBit
        {
            internal int _index;
            internal byte _mask;

            internal HeaderBit(int bitNum)
            {
                _index = bitNum / 8;
                _mask = (byte)(1 << (bitNum % 8));
            }

            internal void AddToMask(ref byte[] mask)
            {
                if (mask == null)
                {
                    mask = new byte[_index + 1];
                }
                else if (mask.Length <= _index)
                {
                    Array.Resize(ref mask, _index + 1);
                }

                mask[_index] |= _mask;
            }
        }

        private class Attr : IComparable<Attr>
        {
            internal string _local;
            internal string _ns;
            internal string _val;
            private readonly string _key;

            internal Attr(string l, string ns, string v)
            {
                _local = l;
                _ns = ns;
                _val = v;
                _key = ns + ":" + l;
            }

            public int CompareTo(Attr a)
            {
                return string.Compare(_key, a._key, StringComparison.Ordinal);
            }
        }
    }
}
