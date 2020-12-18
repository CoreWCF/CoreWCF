using CoreWCF;
using System.Threading.Tasks;

namespace CoreWCFPerfService
{
    [ServiceContract]
    public interface ISayHello
    {
        [OperationContract]
        string Hello(string name);
    }

}
