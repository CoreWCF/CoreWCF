using System.ServiceModel;
using System.Threading.Tasks;

namespace CoreWCFPerf
{
    [ServiceContract]
    public interface ISayHello
    {
        [OperationContract]
        string HelloAsync(string name);
    }
}
