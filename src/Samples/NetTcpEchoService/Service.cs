using CoreWCF;
using System.IO;
using System.Threading.Tasks;

namespace NetTcpEchoServiceSample
{
    internal static class Constants
    {
        public const string NS = "http://tempuri.org/";
        public const string ECHOSERVICE_NAME = nameof(IEchoService);
        public const string OPERATION_BASE = NS + ECHOSERVICE_NAME + "/";
    }

    [ServiceContract(Namespace = "http://tempuri.org/", Name = "IEchoService")]
    public interface IEchoService
    {
        [OperationContract]
        string EchoString(string echo);
    }

    public class EchoService : IEchoService
    {
        public string EchoString(string echo)
        {
            return echo;
        }
    }
}
