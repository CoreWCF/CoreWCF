using CoreWCF;

namespace Contract
{
    [ServiceContract]
    public interface IEchoService
    {
        [OperationContract]
        string Echo(string text);
    }
}
