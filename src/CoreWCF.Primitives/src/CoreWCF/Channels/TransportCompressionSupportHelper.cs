using System;
using System.Collections.Generic;
using System.Text;

namespace CoreWCF.Channels
{
    internal class TransportCompressionSupportHelper : ITransportCompressionSupport
    {
        public bool IsCompressionFormatSupported(CompressionFormat compressionFormat)
        {
            return true;
        }
    }
}
