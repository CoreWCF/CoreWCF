using System;
using CoreWCF.Channels;

namespace CoreWCF
{
    internal interface IServiceChannel : IContextChannel
    {
        Uri ListenUri { get; }
    }
}