using CoreWCF.Channels;

namespace CoreWCF.Dispatcher
{
    internal interface ICallContextInitializer
    {
        object BeforeInvoke(InstanceContext instanceContext, IClientChannel channel, Message message);
        void AfterInvoke(object correlationState);
    }
}