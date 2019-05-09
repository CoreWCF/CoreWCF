using System;
using Microsoft.ServiceModel.Channels;

namespace Microsoft.ServiceModel.Dispatcher
{
    public interface IErrorHandler
    {
        void ProvideFault(Exception error, MessageVersion version, ref Message fault);
        bool HandleError(Exception error);
    }
}