using System.IO;
using CoreWCF;
using CoreWCF.Channels;

namespace ServiceContract
{
    #region Contract w/ FaultContract on Operations (ITestFaultOpContract)
    [ServiceContract]
    public interface ITestFaultOpContract
    {
        [OperationContract]
        [FaultContract(typeof(string))]
        string TwoWay_Method(string s);

        [OperationContract]
        [FaultContract(typeof(string))]
        void TwoWayVoid_Method(string s);

        [OperationContract]
        [FaultContract(typeof(string))]        
        Stream TwoWayStream_Method(Stream s);

        [OperationContract(AsyncPattern = true)]
        [FaultContract(typeof(string))]
        System.Threading.Tasks.Task<string> TwoWayAsync_MethodAsync(string s);

        [OperationContract]
        [FaultContract(typeof(string))]
        FaultMsgContract MessageContract_Method(FaultMsgContract fmc);

        [OperationContract]
        [FaultContract(typeof(string))]
        Message Untyped_Method(Message m);

        [OperationContract]
        [FaultContract(typeof(string))]
        Message Untyped_MethodReturns(Message m);
    }
    #endregion

    [ServiceContract(Name = "ITestFaultOpContract")]
    public interface ITestFaultOpContractTypedClient
    {
        [OperationContract]
        [FaultContract(typeof(string))]
        string Untyped_Method(string s);

        [OperationContract]
        [FaultContract(typeof(string))]
        string Untyped_MethodReturns(string s);
    }

    #region MessageContract (FaultMsgContract)
    [MessageContract]
    public class FaultMsgContract
    {
        [MessageHeader]
        public int ID;

        [MessageBodyMember]
        public string Name;

        public FaultMsgContract()
        {
        }
    }
    #endregion
}
