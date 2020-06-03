using System.ServiceModel;

namespace ClientContract
{
    [ServiceContract]
    interface IOpContractInvalidAction
    {
        [OperationContract(Action = null)]
        void TestMethodNullAction(int id);
    }

    [ServiceContract]
    interface IOpContractInvalidReplyAction
    {
        [OperationContract(ReplyAction = null)]
        int TestMethodNullReplyAction(int id);
    }
}
