using Microsoft.ServiceModel.Channels;

namespace Microsoft.ServiceModel.Dispatcher
{
    internal interface IDispatchMessageFormatter
    {
        void DeserializeRequest(Message message, object[] parameters);
        Message SerializeReply(MessageVersion messageVersion, object[] parameters, object result);
    }
}