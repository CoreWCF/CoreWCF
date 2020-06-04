using CoreWCF;
using CoreWCF.Channels;

namespace ServiceContract
{
    [ServiceContract]
    public interface ITestBasicScenarios
    {
        [OperationContract]
        string TestMethodDefaults(int ID, string name);

        [OperationContract(Action = "myAction")]
        void TestMethodSetAction(int ID, string name);

        [OperationContract(ReplyAction = "myReplyAction")]
        int TestMethodSetReplyAction(int ID, string name);

        [OperationContract(Action = "myUntypedAction")]
        void TestMethodUntypedAction(Message m);

        [OperationContract(ReplyAction = "myUntypedReplyAction")]
        Message TestMethodUntypedReplyAction();

        [OperationContract(Action = "mySetUntypedAction")]
        void TestMethodSetUntypedAction(Message m);

        [OperationContract(AsyncPattern = true, Action = "myAsyncAction", ReplyAction = "myAsyncReplyAction")]
        //IAsyncResult BeginTestMethodAsync(int ID, string name, AsyncCallback callback, object state);
        System.Threading.Tasks.Task<string> TestMethodAsync(int ID, string name);      
    }
}
