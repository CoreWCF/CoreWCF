using System.IO;
using System.Threading.Tasks;
using CoreWCF;

namespace ServiceContract
{
    [ServiceContract]
    public interface IEcho
    {
        [OperationContract]
        string Echo(string inputValue);
    }
}
