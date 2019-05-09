using Microsoft.ServiceModel.Channels;

namespace Microsoft.ServiceModel.Dispatcher
{
    public interface IClientMessageInspector
    {
        void AfterReceiveReply(ref Message reply, object correlationState);
        object BeforeSendRequest(ref Message request, IClientChannel channel);
    }
}