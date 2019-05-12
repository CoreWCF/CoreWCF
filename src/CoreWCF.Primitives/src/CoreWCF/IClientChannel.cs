using System;
using CoreWCF.Channels;

namespace CoreWCF
{
    public interface IClientChannel : IContextChannel, IDisposable
    {
        event EventHandler<UnknownMessageReceivedEventArgs> UnknownMessageReceived;
    }
}