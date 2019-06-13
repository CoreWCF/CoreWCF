﻿using CoreWCF.Security;
using System;
using System.Net.Security;
using System.Security.Authentication;
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

        internal const CompressionFormat DefaultCompressionFormat = CompressionFormat.None;

        internal static readonly XmlDictionaryReaderQuotas ReaderQuotas = new XmlDictionaryReaderQuotas();

        internal static bool IsDefaultReaderQuotas(XmlDictionaryReaderQuotas quotas)
        {
            return quotas.ModifiedQuotas == 0x00;
        }
    }

    internal static class TextEncoderDefaults
    {
        internal static readonly Encoding Encoding = Encoding.GetEncoding(EncodingString, new EncoderExceptionFallback(),
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


        internal static void ValidateEncoding(Encoding encoding)
        {
            string charSet = encoding.WebName;
            Encoding[] supportedEncodings = SupportedEncodings;
            for (int i = 0; i < supportedEncodings.Length; i++)
            {
                if (charSet == supportedEncodings[i].WebName)
                    return;
            }

            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
                new ArgumentException(SR.Format(SR.MessageTextEncodingNotSupported, charSet), nameof(encoding)));
        }

        internal static string EncodingToCharSet(Encoding encoding)
        {
            string webName = encoding.WebName;
            CharSetEncoding[] charSetEncodings = CharSetEncodings;
            for (int i = 0; i < charSetEncodings.Length; i++)
            {
                Encoding enc = charSetEncodings[i].Encoding;
                if (enc == null)
                    continue;

                if (enc.WebName == webName)
                    return charSetEncodings[i].CharSet;
            }
            return null;
        }

        internal static bool TryGetEncoding(string charSet, out Encoding encoding)
        {
            CharSetEncoding[] charSetEncodings = CharSetEncodings;

            // Quick check for exact equality
            for (int i = 0; i < charSetEncodings.Length; i++)
            {
                if (charSetEncodings[i].CharSet == charSet)
                {
                    encoding = charSetEncodings[i].Encoding;
                    return true;
                }
            }

            // Check for case insensitive match
            for (int i = 0; i < charSetEncodings.Length; i++)
            {
                string compare = charSetEncodings[i].CharSet;
                if (compare == null)
                    continue;

                if (compare.Equals(charSet, StringComparison.OrdinalIgnoreCase))
                {
                    encoding = charSetEncodings[i].Encoding;
                    return true;
                }
            }

            encoding = null;
            return false;
        }

        internal class CharSetEncoding
        {
            internal string CharSet;
            internal Encoding Encoding;

            internal CharSetEncoding(string charSet, Encoding enc)
            {
                CharSet = charSet;
                Encoding = enc;
            }
        }
    }

    static class BinaryEncoderDefaults
    {
        internal static EnvelopeVersion EnvelopeVersion { get { return EnvelopeVersion.Soap12; } }
        internal static BinaryVersion BinaryVersion { get { return BinaryVersion.Version1; } }
        internal const int MaxSessionSize = 2048;
    }

    internal static class TransportDefaults
    {
        internal const bool ExtractGroupsForWindowsAccounts = SspiSecurityTokenProvider.DefaultExtractWindowsGroupClaims;
        internal const bool ManualAddressing = false;
        internal const long MaxReceivedMessageSize = 65536;
        internal const int MaxBufferSize = (int) MaxReceivedMessageSize;
        internal const long MaxBufferPoolSize = 512 * 1024;
        internal const int MaxFaultSize = MaxBufferSize;
        internal const bool RequireClientCertificate = false;
        internal const int TcpUriDefaultPort = 808;

        internal const SslProtocols SslProtocols = System.Security.Authentication.SslProtocols.Tls |
                                                   System.Security.Authentication.SslProtocols.Tls11 |
                                                   System.Security.Authentication.SslProtocols.Tls12;

        internal static MessageEncoderFactory GetDefaultMessageEncoderFactory()
        {
            return new BinaryMessageEncodingBindingElement().CreateMessageEncoderFactory();
        }
    }

    internal static class ConnectionOrientedTransportDefaults
    {
        internal const bool AllowNtlm = SspiSecurityTokenProvider.DefaultAllowNtlm;
        internal const int ConnectionBufferSize = 8192;
        internal const ProtectionLevel ProtectionLevel = System.Net.Security.ProtectionLevel.EncryptAndSign;
    }
}