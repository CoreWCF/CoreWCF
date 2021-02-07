using CoreWCF;
using System.Threading.Tasks;

namespace CoreWCFPerfService
{
    [ServiceContract]
    public interface ISayHello
    {
        [OperationContract]
        Task<string> HelloAsync(string name);
    }

}
