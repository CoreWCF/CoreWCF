using CoreWCF;
using System.Runtime.Serialization;

namespace ServiceContract
{
    [ServiceContract]
    [ServiceKnownType(typeof(HelloReply))]
    interface IServiceKnownTypeTest
    {
        [OperationContract]
        BaseHelloReply SayHello(HelloRequest request);
    }

    [DataContract]
    public class HelloRequest
    {
        [DataMember]
        public string Name { get; set; }
    }

    [DataContract]
    public class HelloReply : BaseHelloReply
    {
        [DataMember]
        public string Message { get; set; }
    }

    [DataContract]
    public class BaseHelloReply
    {
        [DataMember]
        public string Name { get; set; }
    }
}
