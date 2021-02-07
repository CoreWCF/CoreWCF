using System.ServiceModel;
using System.Threading.Tasks;

namespace CoreWCFPerf
{
    [ServiceContract]
    public interface ISayHello
    {
        [OperationContract]
        Task<string> HelloAsync(string name);
    }
}
