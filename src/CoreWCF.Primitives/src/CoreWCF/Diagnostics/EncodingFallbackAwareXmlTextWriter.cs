// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Text;
using System.Xml;

namespace CoreWCF.Diagnostics
{
    class EncodingFallbackAwareXmlTextWriter : XmlTextWriter
    {
        private readonly Encoding _encoding;

        internal EncodingFallbackAwareXmlTextWriter(TextWriter writer) : base(writer)
        {
            _encoding = writer.Encoding;
        }

        public override void WriteString(string value)
        {
            if (!string.IsNullOrEmpty(value) &&
                ContainsInvalidXmlChar(value))
            {
                byte[] blob = _encoding.GetBytes(value);
                value = _encoding.GetString(blob);
            }

            base.WriteString(value);
        }

        bool ContainsInvalidXmlChar(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            int i = 0;
            int len = value.Length;

            while (i < len)
            {
                if (XmlConvert.IsXmlChar(value[i]))
                {
                    i++;
                    continue;
                }

                if (i + 1 < len &&
                    XmlConvert.IsXmlSurrogatePair(value[i + 1], value[i]))
                {
                    i += 2;
                    continue;
                }

                return true;
            }

            return false;
        }
    }
}
