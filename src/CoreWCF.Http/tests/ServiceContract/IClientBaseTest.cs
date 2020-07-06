using CoreWCF;
using System.Runtime.Serialization;

namespace ServiceContract
{
    [ServiceContract(Namespace = "http://microsoft.samples", Name = "IClientBaseTest")]
    public interface IClientBaseTest
    {
    }
    [DataContract]
    public enum BindingTypesToUse
    {
        None = 0,
        BasicHttpBinding = 1,
        WsHttpBinding = 2,
        WsDualHttpBinding = 4,
        NetTcpBinding = 8,
        NetNamedPipeBinding = 16
    }
}
