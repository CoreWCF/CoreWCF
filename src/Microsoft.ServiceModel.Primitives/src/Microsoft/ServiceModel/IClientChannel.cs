using System;
using Microsoft.ServiceModel.Channels;

namespace Microsoft.ServiceModel
{
    public interface IClientChannel : IContextChannel, IDisposable
    {
        event EventHandler<UnknownMessageReceivedEventArgs> UnknownMessageReceived;
    }
}