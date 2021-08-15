using CoreWCF;

namespace ServiceContract
{
    [ServiceContract]
    public interface IMathService
    {
        [OperationContract]
        int Add(int x, int y);

        [OperationContract]
        int Subtract(int x, int y);

        [OperationContract]
        int Multiply(int x, int y);
    }
}
