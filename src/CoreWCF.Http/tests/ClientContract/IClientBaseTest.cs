using System.Runtime.Serialization;
using System.ServiceModel;

namespace ClientContract
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
    public enum BindingType
    {
        WSDualHttpBinding,
        WSHttpBinding,
        BasicHttpBinding,
        NetTcpBinding,
        NetNamedPipeBinding,
        NetMsmqBinding
    }
}
