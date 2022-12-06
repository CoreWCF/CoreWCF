using System.ServiceModel;
using System.Threading.Tasks;

namespace ClientContract
{
    [ServiceContract]
    public interface IOneWayContract
    {
        // Token: 0x06000864 RID: 2148
        [OperationContract(IsOneWay = true)]
        void OneWay(string s);
    }
}
