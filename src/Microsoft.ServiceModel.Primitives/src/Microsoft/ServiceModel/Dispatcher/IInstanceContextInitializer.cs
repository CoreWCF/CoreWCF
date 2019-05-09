using Microsoft.ServiceModel.Channels;

namespace Microsoft.ServiceModel.Dispatcher
{
    internal interface IInstanceContextInitializer
    {
        // message=null for singleton
        void Initialize(InstanceContext instanceContext, Message message);
    }
}