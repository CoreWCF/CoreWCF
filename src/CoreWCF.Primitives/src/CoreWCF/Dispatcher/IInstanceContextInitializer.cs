using CoreWCF.Channels;

namespace CoreWCF.Dispatcher
{
    internal interface IInstanceContextInitializer
    {
        // message=null for singleton
        void Initialize(InstanceContext instanceContext, Message message);
    }
}