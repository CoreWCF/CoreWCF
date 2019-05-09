using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.ServiceModel.Channels
{
    internal class NetFramingTransportSettings : ITransportFactorySettings
    {
        public TimeSpan CloseTimeout { get; set; }
        public TimeSpan OpenTimeout { get; set; }
        public TimeSpan ReceiveTimeout { get; set; }
        public TimeSpan SendTimeout { get; set; }
        public bool ManualAddressing { get; set; }
        public BufferManager BufferManager { get; set; }
        public long MaxReceivedMessageSize { get; set; }
        public MessageEncoderFactory MessageEncoderFactory { get; set; }
        public MessageVersion MessageVersion => MessageEncoderFactory.MessageVersion;
        public IAnonymousUriPrefixMatcher AnonymousUriPrefixMatcher { get; set; }
    }
}
