using CoreWCF;
using System.Runtime.Serialization;

namespace Contract
{
    [ServiceContract]
    public interface IEchoService
    {
        [OperationContract]
        string Echo(string text);

        [OperationContract]
        string ComplexEcho(NetCoreServer.EchoMessage text);
    }
}
