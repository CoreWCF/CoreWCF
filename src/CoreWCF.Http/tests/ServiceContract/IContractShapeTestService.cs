using CoreWCF;
using System.Runtime.Serialization;

namespace ServiceContract
{
    [DataContract(Namespace = "http://Microsoft.ServiceModel.Samples")]
    public class SM_ComplexType
    {
        [DataMember]
        public int n;
        [DataMember]
        public string s;
    }

    [ServiceContract(Name = "IContractService")]
    interface IServiceContract_Overloads
    {
        [OperationContract(Name = "TwoWayInt")]
        string TwoWayMethod(int n);

        [OperationContract(Name = "TwoWayString")]
        string TwoWayMethod(string s);

        [OperationContract(Name = "TwoWayComplex")]
        string TwoWayMethod(SM_ComplexType ct);

        [OperationContract(Name = "TwoWayVoid")]
        string TwoWayMethod();
    }

    [ServiceContract(Name = "IContractService")]
    interface IServiceContract_Params
    {
        // Two Way w/ Parameter Array
        [OperationContract]
        string TwoWayParamArray(int n, params int[] args);
    }
}