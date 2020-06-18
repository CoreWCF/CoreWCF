using CoreWCF;
using CoreWCF.Channels;

namespace ServiceContract
{
    [ServiceContract(Name = "IOpActionReplyActionBehavior")]
    public interface IOpActionReplyActionBehavior
    {
        [OperationContract]
        int TestMethodCheckDefaultReplyAction(int ID, string name);

        [OperationContract(ReplyAction = "myReplyAction")]
        int TestMethodCheckCustomReplyAction(int ID, string name);

        [OperationContract(ReplyAction = "http://myReplyAction")]
        int TestMethodCheckUriReplyAction(int ID, string name);

        [OperationContract(ReplyAction = "")]
        int TestMethodCheckEmptyReplyAction(int ID, string name);

        [OperationContract(ReplyAction = "*")]
        Message TestMethodCheckUntypedReplyAction();

        [OperationContract(IsOneWay = true, Action = "*")]
        void UnMatchedMessageHandler(Message m);
    }
}
