using Microsoft.ServiceModel.Channels;

namespace Microsoft.ServiceModel.Dispatcher
{
    public interface IDispatchMessageInspector
    {
        object AfterReceiveRequest(ref Message request, IClientChannel channel, InstanceContext instanceContext);
        void BeforeSendReply(ref Message reply, object correlationState);
    }
}