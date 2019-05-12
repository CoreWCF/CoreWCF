using System;

namespace CoreWCF.Dispatcher
{
    internal interface IResumeMessageRpc
    {
        InstanceContext GetMessageInstanceContext();

        void Resume();
        void Resume(out bool alreadyResumedNoLock);
        // TODO: Convert to task based continuations
        void Resume(IAsyncResult result);
        void Resume(object instance);
        void SignalConditionalResume(IAsyncResult result);
    }
}