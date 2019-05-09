using System;

namespace Microsoft.ServiceModel
{
    public interface IDefaultCommunicationTimeouts
    {
        TimeSpan CloseTimeout { get; }
        TimeSpan OpenTimeout { get; }
        TimeSpan ReceiveTimeout { get; }
        TimeSpan SendTimeout { get; }
    }
}