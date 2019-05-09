using Microsoft.ServiceModel.Channels;

namespace Microsoft.ServiceModel.Dispatcher
{
    internal interface IClientFaultFormatter
    {
        FaultException Deserialize(MessageFault messageFault, string action);
    }
}