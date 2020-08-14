using CoreWCF.Channels;
using ServiceContract;

namespace Services
{
    public class RemoteEndpointMessagePropertyService : IRemoteEndpointMessageProperty
    {
        public Message Echo(Message input)
        {
            RemoteEndpointMessageProperty remp = (RemoteEndpointMessageProperty)input.Properties[RemoteEndpointMessageProperty.Name];
            return Message.CreateMessage(input.Version, "echo", input.GetBody<string>()+";"+ remp.Address+";"+ remp.Port.ToString());
        }
    }
}
