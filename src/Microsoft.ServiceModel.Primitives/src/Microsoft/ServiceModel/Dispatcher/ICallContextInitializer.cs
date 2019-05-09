using Microsoft.ServiceModel.Channels;

namespace Microsoft.ServiceModel.Dispatcher
{
    internal interface ICallContextInitializer
    {
        object BeforeInvoke(InstanceContext instanceContext, IClientChannel channel, Message message);
        void AfterInvoke(object correlationState);
    }
}