using CoreWCF.Channels;

namespace CoreWCF.Dispatcher
{
    public interface IClientMessageInspector
    {
        void AfterReceiveReply(ref Message reply, object correlationState);
        object BeforeSendRequest(ref Message request, IClientChannel channel);
    }
}