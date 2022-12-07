using System.ServiceModel;
using System.Threading.Tasks;

namespace ClientContract
{
    [ServiceContract]
    public interface IOneWayContract
    {
        // Token: 0x06000864 RID: 2148
        [OperationContract(AsyncPattern = true, IsOneWay = true)]
        Task OneWay(string s);
    }
}
