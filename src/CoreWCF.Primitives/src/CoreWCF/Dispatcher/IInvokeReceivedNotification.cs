using CoreWCF.Channels;

namespace CoreWCF.Dispatcher
{
    interface IInvokeReceivedNotification
    {
        void NotifyInvokeReceived();
        void NotifyInvokeReceived(RequestContext request);
    }
}