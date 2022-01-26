// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Text;
using System.Xml;

namespace CoreWCF.Channels
{
    internal static class EncoderDefaults
    {
        internal const int MaxReadPoolSize = 64;
        internal const int MaxWritePoolSize = 16;

        internal const int BufferedReadDefaultMaxDepth = 128;
        internal const int BufferedReadDefaultMaxStringContentLength = int.MaxValue;
        internal const int BufferedReadDefaultMaxArrayLength = int.MaxValue;
        internal const int BufferedReadDefaultMaxBytesPerRead = int.MaxValue;
        internal const int BufferedReadDefaultMaxNameTableCharCount = int.MaxValue;

        internal static readonly XmlDictionaryReaderQuotas ReaderQuotas = new XmlDictionaryReaderQuotas();

        internal static bool IsDefaultReaderQuotas(XmlDictionaryReaderQuotas quotas)
        {
            return quotas.ModifiedQuotas == 0x00;
        }
    }

    internal static class TransportDefaults
    {
        internal const long MaxReceivedMessageSize = 65536;
        internal const int MaxBufferSize = (int)MaxReceivedMessageSize;
        internal const long MaxBufferPoolSize = 512 * 1024;
    }

    internal static class HttpTransportDefaults
    {
        internal const TransferMode TransferMode = CoreWCF.TransferMode.Buffered;
        internal const string Realm = "";
    }

    internal static class TextEncoderDefaults
    {
        public static readonly Encoding Encoding = Encoding.GetEncoding(EncodingString, new EncoderExceptionFallback(),
            new DecoderExceptionFallback());

        internal const string EncodingString = "utf-8";

        internal static readonly Encoding[] SupportedEncodings =
        {
            Encoding.UTF8, Encoding.Unicode,
            Encoding.BigEndianUnicode
        };

        internal static readonly CharSetEncoding[] CharSetEncodings =
        {
            new CharSetEncoding("utf-8", Encoding.UTF8),
            new CharSetEncoding("utf-16LE", Encoding.Unicode),
            new CharSetEncoding("utf-16BE", Encoding.BigEndianUnicode),
            new CharSetEncoding("utf-16", null), // Ignore.  Ambiguous charSet, so autodetect.
            new CharSetEncoding(null, null), // CharSet omitted, so autodetect.
        };


        public static void ValidateEncoding(Encoding encoding)
        {
            string charSet = encoding.WebName;
            Encoding[] supportedEncodings = SupportedEncodings;
            for (int i = 0; i < supportedEncodings.Length; i++)
            {
                if (charSet == supportedEncodings[i].WebName)
                {
                    return;
                }
            }

            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                new ArgumentException(SR.Format(SR.MessageTextEncodingNotSupported, charSet), nameof(encoding)));
        }

        public static string EncodingToCharSet(Encoding encoding)
        {
            string webName = encoding.WebName;
            CharSetEncoding[] charSetEncodings = CharSetEncodings;
            for (int i = 0; i < charSetEncodings.Length; i++)
            {
                Encoding enc = charSetEncodings[i]._encoding;
                if (enc == null)
                {
                    continue;
                }

                if (enc.WebName == webName)
                {
                    return charSetEncodings[i]._charSet;
                }
            }
            return null;
        }

        public static bool TryGetEncoding(string charSet, out Encoding encoding)
        {
            CharSetEncoding[] charSetEncodings = CharSetEncodings;

            // Quick check for exact equality
            for (int i = 0; i < charSetEncodings.Length; i++)
            {
                if (charSetEncodings[i]._charSet == charSet)
                {
                    encoding = charSetEncodings[i]._encoding;
                    return true;
                }
            }

            // Check for case insensitive match
            for (int i = 0; i < charSetEncodings.Length; i++)
            {
                string compare = charSetEncodings[i]._charSet;
                if (compare == null)
                {
                    continue;
                }

                if (compare.Equals(charSet, StringComparison.OrdinalIgnoreCase))
                {
                    encoding = charSetEncodings[i]._encoding;
                    return true;
                }
            }

            encoding = null;
            return false;
        }

        internal class CharSetEncoding
        {
            internal string _charSet;
            internal Encoding _encoding;

            internal CharSetEncoding(string charSet, Encoding enc)
            {
                _charSet = charSet;
                _encoding = enc;
            }
        }
    }
}
