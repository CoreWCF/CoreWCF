using CoreWCF;

namespace ServiceContract
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
