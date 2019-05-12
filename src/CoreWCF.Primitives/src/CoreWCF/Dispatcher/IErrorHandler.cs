using System;
using CoreWCF.Channels;

namespace CoreWCF.Dispatcher
{
    public interface IErrorHandler
    {
        void ProvideFault(Exception error, MessageVersion version, ref Message fault);
        bool HandleError(Exception error);
    }
}