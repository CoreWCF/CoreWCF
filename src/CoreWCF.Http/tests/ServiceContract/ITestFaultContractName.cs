using System.IO;
using CoreWCF;
using CoreWCF.Channels;

namespace ServiceContract
{
    #region Contract w/ FaultContract on Operations (ITestFaultContractName)
    [ServiceContract]
    public interface ITestFaultContractName
    {
        [OperationContract]
        [FaultContract(typeof(string), Name = "Method1")]
        string Method1(string s);

        [OperationContract]
        [FaultContract(typeof(string), Name = "Method2")]
        string Method2(string s);

        [OperationContract]
        [FaultContract(typeof(string), Name = "ITestFaultContractAction.Method3")]
        string Method3(string s);

        [OperationContract]
        [FaultContract(typeof(string), Name = "<hello>\"\'")]
        string Method4(string s);

        [OperationContract]
        [FaultContract(typeof(string), Name = "t t n")]
        string Method5(string s);

        [OperationContract]
        [FaultContract(typeof(string), Name = "&lt;&gt;&gt;")]
        string Method6(string s);

        [OperationContract]
        [FaultContract(typeof(string), Name = "http://y.c/5")]
        string Method7(string s);

        [OperationContract]
        [FaultContract(typeof(string), Name = "İüğmeIiiçeI")]
        string Method8(string s);

        [OperationContract]
        [FaultContract(typeof(string), Name = "esÅeoplÀÁð")]
        string Method9(string s);

        [OperationContract]
        [FaultContract(typeof(string), Name = "http://hello/\0")]
        string Method10(string s);

        [OperationContract]
        [FaultContract(typeof(string), Name = "http://www.yahoo.com")]
        string Method11(string s);

        [OperationContract]
        [FaultContract(typeof(string), Name = "null")]
        string Method12(string s);

        [OperationContract]
        [FaultContract(typeof(string), Name = "<hello>\"\'\0")]
        string Method13(string s);

        [OperationContract]
        [FaultContract(typeof(string), Name = "esÅeoplÀÁð")]
        string Method14(string s);

        [OperationContract]
        [FaultContract(typeof(string), Name = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
        string Method15(string s);

        [OperationContract]
        [FaultContract(typeof(string), Name = "123456789012345")]
        string Method16(string s);

        [OperationContract]
        [FaultContract(typeof(string), Name = "u")]
        string Method17(string s);

        [OperationContract]
        [FaultContract(typeof(string), Name = "ÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅÅ")]
        string Method18(string s);

        [OperationContract]
        [FaultContract(typeof(string), Name = "a_b - c - d_g()")]
        string Method19(string s);

        [OperationContract]
        [FaultContract(typeof(string), Name = "\\//\\/////////// /")]
        string Method20(string s);

        [OperationContract]
        [FaultContract(typeof(string), Name = "*")]
        string Method21(string s);
    }
    #endregion
}
