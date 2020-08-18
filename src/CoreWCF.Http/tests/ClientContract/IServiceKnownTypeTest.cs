using System.Runtime.Serialization;
using System.ServiceModel;
using System.Text;

namespace ClientContract
{
    [ServiceContract]
    [ServiceKnownType(typeof(HelloReply))]
    interface IServiceKnownTypeTest
    {
        [OperationContract]
        BaseHelloReply SayHello(HelloRequest request);
    }

    [DataContract(Namespace = "http://schemas.datacontract.org/2004/07/ServiceContract")]
    public class HelloRequest
    {
        [DataMember]
        public string Name { get; set; }
    }

    [DataContract(Namespace = "http://schemas.datacontract.org/2004/07/ServiceContract")]
    public class HelloReply : BaseHelloReply
    {
        [DataMember]
        public string Message { get; set; }
    }

    [DataContract(Namespace = "http://schemas.datacontract.org/2004/07/ServiceContract")]
    public class BaseHelloReply
    {
        [DataMember]
        public string Name { get; set; }
    }
}
