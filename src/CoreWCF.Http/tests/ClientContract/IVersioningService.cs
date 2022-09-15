using ServiceContract;
using System.ServiceModel;

namespace ClientContract
{
    [ServiceContract(Name = "IVersioningServiceNew")]
    public interface IVersioningClientOld
    {
        [OperationContract(Name = "Method_NewContractA_out")]
        void Method_OldContractA_out(OldContractA obj1, out OldContractA obj2);

        [OperationContract(Name = "Method_NewContractA_ref")]
        OldContractA Method_OldContractA_ref(ref OldContractA obj1);

        [OperationContract(Name = "Method_NewContractA")]
        OldContractA Method_OldContractA(OldContractA obj1);
    }

    [ServiceContract(Name = "IVersioningServiceOld")]
    public interface IVersioningClientNew
    {
        [OperationContract(Name = "Method_OldContractA_out")]
        void Method_NewContractA_out(NewContractA obj1, out NewContractA obj2);

        [OperationContract(Name = ("Method_OldContractA_ref"))]
        NewContractA Method_NewContractA_ref(ref NewContractA obj1);

        [OperationContract(Name = "Method_OldContractA")]
        NewContractA Method_NewContractA(NewContractA obj1);
    }
}