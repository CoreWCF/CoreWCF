using System.ServiceModel;

namespace ClientContract
{
    [ServiceContract]
    public interface ITestFaultContractName1
    {
        [OperationContract]
        [FaultContract(typeof(string), Name = "foo")]
        string Method1(string s);
    }
}
