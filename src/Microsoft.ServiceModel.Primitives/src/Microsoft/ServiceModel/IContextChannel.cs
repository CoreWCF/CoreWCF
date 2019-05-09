using System;
using Microsoft.ServiceModel.Channels;

namespace Microsoft.ServiceModel
{
    public interface IContextChannel : IChannel, IExtensibleObject<IContextChannel>
    {
        //bool AllowOutputBatching { get; set; }
        IInputSession InputSession { get; }
        EndpointAddress LocalAddress { get; }
        TimeSpan OperationTimeout { get; set; }
        IOutputSession OutputSession { get; }
        EndpointAddress RemoteAddress { get; }
        string SessionId { get; }
    }
}