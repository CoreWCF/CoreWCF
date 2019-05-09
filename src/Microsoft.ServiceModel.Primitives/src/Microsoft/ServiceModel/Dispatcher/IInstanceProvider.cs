using Microsoft.ServiceModel.Channels;

namespace Microsoft.ServiceModel.Dispatcher
{
    internal interface IInstanceProvider
    {
        object GetInstance(InstanceContext instanceContext);
        object GetInstance(InstanceContext instanceContext, Message message);
        void ReleaseInstance(InstanceContext instanceContext, object instance);
    }
}