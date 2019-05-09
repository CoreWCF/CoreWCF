using Microsoft.ServiceModel.Channels;

namespace Microsoft.ServiceModel.Dispatcher
{
    interface IInvokeReceivedNotification
    {
        void NotifyInvokeReceived();
        void NotifyInvokeReceived(RequestContext request);
    }
}