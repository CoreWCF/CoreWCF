using System.Runtime.Serialization;
using System.ServiceModel;

namespace Contract
{
    [ServiceContract]
    public interface IEchoService
    {
        [OperationContract]
        string Echo(string text);

        [OperationContract]
        string ComplexEcho(NetCoreClient.EchoMessage text);
    }
}
