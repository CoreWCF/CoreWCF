using System.ServiceModel;
using System.ServiceModel.Channels;

namespace ClientContract
{
    [ServiceContract]
    public interface IRemoteEndpointMessageProperty
    {
        [OperationContract(Action = "*", ReplyAction = "*")]
        Message Echo(Message input);
    }
}
