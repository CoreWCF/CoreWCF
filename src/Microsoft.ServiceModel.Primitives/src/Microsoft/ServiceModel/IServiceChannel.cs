using System;
using Microsoft.ServiceModel.Channels;

namespace Microsoft.ServiceModel
{
    internal interface IServiceChannel : IContextChannel
    {
        Uri ListenUri { get; }
    }
}