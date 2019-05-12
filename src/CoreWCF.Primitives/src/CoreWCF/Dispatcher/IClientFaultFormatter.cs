using CoreWCF.Channels;

namespace CoreWCF.Dispatcher
{
    internal interface IClientFaultFormatter
    {
        FaultException Deserialize(MessageFault messageFault, string action);
    }
}