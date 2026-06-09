using CoreWCF;

namespace ServiceContract
{
    [ServiceContract]
    public interface ILifeTimeTestService
    {
        [OperationContract]
        void Start();

        [OperationContract(IsOneWay = true)]
        void OneWay();

        [OperationContract]
        void TwoWay();

        [OperationContract]
        void Final(int onewaycalls, string variation);
    }
}
