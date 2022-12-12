// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace CoreWCF.Channels
{
    internal static class FramingUpgradeString
    {
        public const string SslOrTls = "application/ssl-tls";
        public const string Negotiate = "application/negotiate";
    }

    internal static class FramingEncodingString
    {
        public const string Binary = "application/soap+msbin1";
        public const string BinarySession = "application/soap+msbinsession1";
        public const string ExtendedBinaryGZip = Binary + "+gzip";
        public const string ExtendedBinarySessionGZip = BinarySession + "+gzip";
        public const string ExtendedBinaryDeflate = Binary + "+deflate";
        public const string ExtendedBinarySessionDeflate = BinarySession + "+deflate";
    }
}
