using System;
using System.Collections.Generic;
using System.Text;

namespace CoreWCF.Channels
{
    static class FramingEncodingString
    {
        public const string Binary = "application/soap+msbin1";
        public const string BinarySession = "application/soap+msbinsession1";
        public const string ExtendedBinaryGZip = Binary + "+gzip";
        public const string ExtendedBinarySessionGZip = BinarySession + "+gzip";
        public const string ExtendedBinaryDeflate = Binary + "+deflate";
        public const string ExtendedBinarySessionDeflate = BinarySession + "+deflate";
    }
}
