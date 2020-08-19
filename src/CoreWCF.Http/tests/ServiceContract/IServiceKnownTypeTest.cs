using CoreWCF;
using System.Runtime.Serialization;

namespace ServiceContract
{
    [ServiceContract]
    public interface IServiceKnownTypeBase
    {
        [OperationContract(Action = "http://tempuri.org/IServiceKnownTypeTest/SayHello",
                           ReplyAction = "http://tempuri.org/IServiceKnownTypeTest/SayHelloResponse")]
        BaseHelloReply SayHello(HelloRequest request);
    }

    // Helper interface to avoid having lots of interfaces declared on the service itself
    public interface IServiceKnownTypeTest : IServiceKnownTypeWithType, IServiceKnownTypeWithDeclaredTypeAndMethodName
    { }

    [ServiceContract]
    [ServiceKnownType(typeof(HelloReply))]
    public interface IServiceKnownTypeWithType : IServiceKnownTypeBase
    {
    }

    [ServiceContract]
    [ServiceKnownType("GetKnownTypes", typeof(Services.ServiceKnownTypeServiceHelper))]
    public interface IServiceKnownTypeWithDeclaredTypeAndMethodName : IServiceKnownTypeBase
    {
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
