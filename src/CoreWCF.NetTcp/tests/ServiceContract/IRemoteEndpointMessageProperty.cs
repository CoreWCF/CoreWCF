using CoreWCF;
using CoreWCF.Channels;

namespace ServiceContract
{
    [ServiceContract]
    public interface IRemoteEndpointMessageProperty
    {
        [OperationContract(Action = "*", ReplyAction = "*")]
        Message Echo(Message input);
    }
}
