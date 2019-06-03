using System;
using System.Collections.Generic;
using System.Text;

namespace CoreWCF.Channels
{
    // TODO: Make this internal again or get rid of it, probably through DI or Features
    public interface ITransportFactorySettings : IDefaultCommunicationTimeouts
    {
        bool ManualAddressing { get; }
        BufferManager BufferManager { get; }
        long MaxReceivedMessageSize { get; }
        MessageEncoderFactory MessageEncoderFactory { get; }
        MessageVersion MessageVersion { get; }
    }
}
