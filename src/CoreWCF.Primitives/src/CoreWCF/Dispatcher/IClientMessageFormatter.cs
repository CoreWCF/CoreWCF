using CoreWCF.Channels;

namespace CoreWCF.Dispatcher
{
    public interface IClientMessageFormatter
    {
        Message SerializeRequest(MessageVersion messageVersion, object[] parameters);
        object DeserializeReply(Message message, object[] parameters);
    }
}